﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Servant.Common.Entities;
using Servant.End2EndTests.Core;
using Servant.End2EndTests.Helpers;
using Servant.End2EndTests.Helpers.FileSystem;
using Xunit;

namespace Servant.End2EndTests.ApiTests
{
    public class FileSystemTests
    {
        private readonly RestApiTestClient _restApiClient;

        public FileSystemTests()
        {
            _restApiClient = new RestApiTestClient(new Uri("http://localhost:8025"));
        }

        [Fact]
        public async void When_SendingGetWithoutPath_Should_Return400AndValidationMessage()
        {
            var result = await _restApiClient.Get("/api/fs/get");
            var jcontent = JParser.ParseContent(result.Content);

            Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
            Assert.NotNull(jcontent["ModelState"]["Path"]);
        }

        [Fact]
        public async void When_SendingGetWithPathContainingInvalidCharacters_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(new FileItem("1.txt"));
                var filepath = $"{fs.TempPath}/<1>.txt";
                var result = await _restApiClient.Get($"/api/fs/get?path={filepath}");
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.NotNull(jcontent["ModelState"]["Path"]);
            }
        }

        [Fact]
        public async void When_SendingGetWithNonRootedPath_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var filepath = "dir/1.txt";
                var result = await _restApiClient.Get($"/api/fs/get?path={filepath}");
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.NotNull(jcontent["ModelState"]["Path"]);
            }
        }

        [Fact]
        public async void When_GettingNonExistentFile_Should_Return404()
        {
            using (var fs = new FsInitializer())
            {
                var result = await _restApiClient.Get($"/api/fs/get?path={fs.TempPath}/non_existent_file.txt");
                Assert.Equal(HttpStatusCode.NotFound, result.Response.StatusCode);
            }
        }

        [Fact]
        public async void When_GettingExistingRootedFile_Should_ReturnFileContent()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt", "TEST CONTENT")));
                var filepath = Path.Combine(fs.TempPath, "dir/1.txt");
                var result = await _restApiClient.Get($"/api/fs/get?path={filepath}");

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.IsType(typeof (StreamContent), result.Response.Content);
                Assert.Equal("TEST CONTENT", result.Content);
            }
        }

        [Fact]
        public async void When_GettingFileWithoutDownloadParameter_Should_SetMediaTypeByFileExtension()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt"),
                        new FileItem("1.xml")));
                var filepath1 = Path.Combine(fs.TempPath, "dir/1.txt");
                var filepath2 = Path.Combine(fs.TempPath, "dir/1.xml");

                var result1 = await _restApiClient.Get($"/api/fs/get?path={filepath1}");
                var result2 = await _restApiClient.Get($"/api/fs/get?path={filepath2}");

                Assert.Equal("text/plain", result1.Response.Content.Headers.ContentType.MediaType);
                Assert.Equal("text/xml", result2.Response.Content.Headers.ContentType.MediaType);
            }
        }

        [Fact]
        public async void When_GettingFileWithoutDownloadParameter_Should_SetContentDispositionInline()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var filepath = Path.Combine(fs.TempPath, "dir/1.txt");
                var result = await _restApiClient.Get($"/api/fs/get?path={filepath}");
                Assert.Equal("inline", result.Response.Content.Headers.ContentDisposition.DispositionType);
            }
        }

        [Fact]
        public async void When_GettingFileWithDownloadParameter_Should_SetBinaryMediaType()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt"),
                        new FileItem("1.xml")));
                var filepath1 = Path.Combine(fs.TempPath, "dir/1.txt");
                var filepath2 = Path.Combine(fs.TempPath, "dir/1.xml");

                var result1 = await _restApiClient.Get($"/api/fs/get?path={filepath1}&download=1");
                var result2 = await _restApiClient.Get($"/api/fs/get?path={filepath2}&download=1");

                Assert.Equal("application/octet-stream", result1.Response.Content.Headers.ContentType.MediaType);
                Assert.Equal("application/octet-stream", result2.Response.Content.Headers.ContentType.MediaType);
            }
        }

        [Fact]
        public async void When_GettingFileWithDownloadParameter_Should_SetContentDispositionAttachment()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var filepath = Path.Combine(fs.TempPath, "dir/1.txt");
                var result = await _restApiClient.Get($"/api/fs/get?path={filepath}&download=1");
                Assert.Equal("attachment", result.Response.Content.Headers.ContentDisposition.DispositionType);
            }
        }

        [Fact]
        public async void When_GettingDrives_Should_ReturnListOfDriveInfo()
        {
            var result = await _restApiClient.Get("/api/fs/drives");
            var drives = JsonConvert.DeserializeObject<List<DriveInfoModel>>(result.Content);

            Assert.NotNull(drives);
            Assert.True(drives.Any(x => x.Name.Equals("C:\\", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        }

        [Fact]
        public async void When_PostingCopyActionWithoutAction_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var values = new KeyValueList<string, string>
                {
                    {"sourcePath", Path.Combine(fs.TempPath, "dir/1.txt")},
                    {"destPath", Path.Combine(fs.TempPath, "dir/2.txt")}
                };
                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.Equal("A value for action is not provided.", jcontent["Message"]);
            }
        }

        [Fact]
        public async void When_PostingCopyActionWithUnknownAction_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var values = new KeyValueList<string, string>
                {
                    {"action", "unknown"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir/1.txt")},
                    {"destPath", Path.Combine(fs.TempPath, "dir/2.txt")}
                };
                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.Equal("Unknown action command - 'unknown'.", jcontent["Message"]);
            }
        }

        [Fact]
        public async void When_PostingCopyActionWithoutSourcePath_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"destPath", Path.Combine(fs.TempPath, "dir/2.txt")}
                };
                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.NotNull(jcontent["ModelState"]["SourcePath"]);
            }
        }

        [Fact]
        public async void When_PostingCopyActionWithoutDestPath_Should_Return400AndValidationMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt")));
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir/1.txt")}
                };
                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
                Assert.NotNull(jcontent["ModelState"]["DestPath"]);
            }
        }

        [Fact]
        public async void When_CopyingExistingFile_Should_CopyIt()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt", "Hello Test")));

                var sourcePath = Path.Combine(fs.TempPath, "dir/1.txt");
                var destPath = Path.Combine(fs.TempPath, "dir/2.txt");
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", sourcePath},
                    {"destPath", destPath}
                };

                Assert.False(File.Exists(destPath));

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.True(File.Exists(sourcePath));
                Assert.True(File.Exists(destPath));
                Assert.Equal(File.ReadAllText(destPath), "Hello Test");
            }
        }

        [Fact]
        public async void When_CopyingNonExistingFile_Should_Return404()
        {
            using (var fs = new FsInitializer())
            {
                var sourcePath = Path.Combine(fs.TempPath, "non_existing.txt");
                var destPath = Path.Combine(fs.TempPath, "dir/2.txt");
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", sourcePath},
                    {"destPath", destPath}
                };

                Assert.False(File.Exists(destPath));

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.NotFound, result.Response.StatusCode);
                Assert.False(File.Exists(sourcePath));
                Assert.False(File.Exists(destPath));
            }
        }

        [Fact]
        public async void When_CopyingFileToAlreadyExistingPathWithoutOverwrite_Should_Return500AndExceptionMessage()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt"),
                        new FileItem("2.txt")));

                var sourcePath = Path.Combine(fs.TempPath, "dir/1.txt");
                var destPath = Path.Combine(fs.TempPath, "dir/2.txt");
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", sourcePath},
                    {"destPath", destPath}
                };

                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.InternalServerError, result.Response.StatusCode);
                Assert.True(File.Exists(destPath));
                Assert.Equal("System.IO.IOException", jcontent["ExceptionType"]);
                Assert.NotNull(jcontent["ExceptionMessage"]);
            }
        }

        [Fact]
        public async void When_CopyingFileToAlreadyExistingPathAndOverwriteSetToTrue_Should_CopyIt()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new FileItem("1.txt", "source_content"),
                        new FileItem("2.txt", "dest_content")));

                var sourcePath = Path.Combine(fs.TempPath, "dir/1.txt");
                var destPath = Path.Combine(fs.TempPath, "dir/2.txt");
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", sourcePath},
                    {"destPath", destPath},
                    {"overwrite", "true"}
                };

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.True(File.Exists(destPath));
                Assert.Equal("source_content", File.ReadAllText(destPath));
            }
        }

        [Fact]
        public async void When_CopyingEmptyDirectory_Should_CopyIt()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(new DirItem("dir"));
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir")},
                    {"destPath", Path.Combine(fs.TempPath, "dir2")}
                };

                Assert.False(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));
            }
        }

        [Fact]
        public async void When_CopyingDirectoryContainingFilesAndDirs_Should_CopyEntireDirectory()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new DirItem("subdir",
                            new FileItem("2.txt")),
                        new FileItem("1.txt")));
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir")},
                    {"destPath", Path.Combine(fs.TempPath, "dir2")}
                };

                Assert.False(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);

                // verify source
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir\\subdir")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir\\subdir\\2.txt")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir\\1.txt")));

                // verify destination
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2\\subdir")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\subdir\\2.txt")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\1.txt")));
            }
        }

        [Fact]
        public async void When_CopyingAlreadyExistingDirectoryWithoutOverwrite_Should_Return500AndNotChangeDestination()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir1",
                        new DirItem("subdir"),
                        new FileItem("1.txt", "source_content")),
                    new DirItem("dir2",
                        new DirItem("subdir"),
                        new FileItem("1.txt", "dest_content")));

                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir1")},
                    {"destPath", Path.Combine(fs.TempPath, "dir2")}
                };

                var result = await _restApiClient.Post("/api/fs", values);
                var jcontent = JParser.ParseContent(result.Content);

                Assert.Equal(HttpStatusCode.InternalServerError, result.Response.StatusCode);
                Assert.Equal("System.IO.IOException", jcontent["ExceptionType"]);
                Assert.NotNull(jcontent["ExceptionMessage"]);
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2\\subdir")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\1.txt")));
                Assert.Equal("dest_content", File.ReadAllText(Path.Combine(fs.TempPath, "dir2\\1.txt")));
            }
        }

        [Fact]
        public async void When_CopyingAlreadyExistingDirectoryWithOverwriteSetToTrue_Should_CopyEntireDirectory()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir1",
                        new DirItem("subdir"),
                        new FileItem("1.txt", "source_content")),
                    new DirItem("dir2",
                        new DirItem("subdir"),
                        new DirItem("subdir2"),
                        new FileItem("1.txt", "dest_content")));

                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", Path.Combine(fs.TempPath, "dir1")},
                    {"destPath", Path.Combine(fs.TempPath, "dir2")},
                    {"overwrite", "true"}
                };

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2\\subdir")));
                Assert.False(Directory.Exists(Path.Combine(fs.TempPath, "dir2\\subdir2")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\1.txt")));
                Assert.Equal("source_content", File.ReadAllText(Path.Combine(fs.TempPath, "dir2\\1.txt")));
            }
        }

        [Fact]
        public async void When_CopyingDirectoryByUNCPath_Should_CopyEntireDirectory()
        {
            using (var fs = new FsInitializer())
            {
                fs.CreateItems(
                    new DirItem("dir",
                        new DirItem("subdir",
                            new FileItem("2.txt")),
                        new FileItem("1.txt")));

                var uncTempPath = fs.TempPath.Replace(@"C:\", @"\\localhost\C$\");
                var sourcePath = Path.Combine(uncTempPath, "dir");
                var destPath = Path.Combine(uncTempPath, "dir2");
                var values = new KeyValueList<string, string>
                {
                    {"action", "COPY"},
                    {"sourcePath", sourcePath},
                    {"destPath", destPath}
                };

                Assert.False(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));

                var result = await _restApiClient.Post("/api/fs", values);

                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);

                // verify source
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir\\subdir")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir\\subdir\\2.txt")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir\\1.txt")));

                // verify destination
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2")));
                Assert.True(Directory.Exists(Path.Combine(fs.TempPath, "dir2\\subdir")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\subdir\\2.txt")));
                Assert.True(File.Exists(Path.Combine(fs.TempPath, "dir2\\1.txt")));
            }
        }
    }
}
