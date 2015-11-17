using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Kudu.Services.Jobs
{
    public class FunctionsController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private const int BufferSize = 32 * 1024;

        public FunctionsController(ITracer tracer, IEnvironment environment)
        {
            _tracer = tracer;
            _environment = environment;
        }

        [HttpPut]
        public async Task<HttpResponseMessage> CreateOrUpdate(string name)
        {
            using (_tracer.Step($"FunctionsController.CreateOrUpdate({name})"))
            {
                var config = await Request.Content.ReadAsStringAsync();
                var functionDir = FileSystemHelpers.EnsureDirectory(Path.Combine(_environment.FunctionsPath, name));
                await FileSystemHelpers.WriteAllTextToFileAsync(Path.Combine(functionDir, Constants.FunctionsConfigFile), config);
                return Request.CreateResponse(HttpStatusCode.Created, (object)await GetFunctionConfig(name));
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> List()
        {
            using (_tracer.Step("FunctionsController.list()"))
            {
                var configList = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(FileSystemHelpers.FileExists)
                    .Select(f => GetFunctionConfig(Path.GetFileName(Path.GetDirectoryName(f)))));

                return Request.CreateResponse(HttpStatusCode.OK, configList);
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> Get(string name)
        {
            using (_tracer.Step($"FunctionsController.Get({name})"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, (object)await GetFunctionConfig(name));
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string name)
        {
            using (_tracer.Step($"FunctionsController.Delete({name})"))
            {
                var path = GetFunctionPath(name);
                FileSystemHelpers.DeleteDirectorySafe(path, ignoreErrors: false);
                return Request.CreateResponse(HttpStatusCode.NoContent);
            }
        }

        [HttpPost]
        public Task<HttpResponseMessage> Run(string name)
        {
            using (_tracer.Step($"FunctionsController.Run({name})"))
            {
                throw new NotImplementedException();
            }
        }

        [HttpGet]
        public Task<HttpResponseMessage> GetRunStatus(string id)
        {
            using (_tracer.Step($"FunctionsController.GetRunStatus({id})"))
            {
                throw new NotImplementedException();
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetHostSettings()
        {
            using (_tracer.Step("FunctionsController.GetHostSettings()"))
            {
                var path = Path.Combine(_environment.FunctionsPath, Constants.FunctionsConfigFile);
                if (FileSystemHelpers.FileExists(path))
                {
                    var config = JsonConvert.DeserializeObject(await FileSystemHelpers.ReadAllTextFromFileAsync(path));
                    return Request.CreateResponse(HttpStatusCode.OK, config);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> PutHostSettings()
        {
            using (_tracer.Step("FunctionsController.PutHostSettings()"))
            {
                var path = Path.Combine(_environment.FunctionsPath, Constants.FunctionsConfigFile);
                await FileSystemHelpers.WriteAllTextToFileAsync(path, await Request.Content.ReadAsStringAsync());
                return Request.CreateResponse(HttpStatusCode.Created);
            }
        }

        public async Task<dynamic> GetFunctionConfig(string name)
        {
            var path = Path.Combine(GetFunctionPath(name), Constants.FunctionsConfigFile);
            if (FileSystemHelpers.FileExists(path))
            {
                return CreateFunctionConfig(await FileSystemHelpers.ReadAllTextFromFileAsync(path), name);
            }

            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        public dynamic CreateFunctionConfig(string configContent, string functionName)
        {
            var config = JsonConvert.DeserializeObject<dynamic>(configContent);
            config.name = functionName;
            config.script_href = FilePathToVfsUri(Path.Combine(GetFunctionPath(functionName), Constants.FunctionsScriptFile));
            return config;
        }

        public Uri FilePathToVfsUri(string filePath)
        {
            var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            filePath = filePath.Substring(_environment.RootPath.Length).Trim('\\').Replace("\\", "/");
            return new Uri($"{baseUrl}/api/vfs/{filePath}");
        }

        public string GetFunctionPath(string name)
        {
            var path = Path.Combine(_environment.FunctionsPath, name);
            if (FileSystemHelpers.DirectoryExists(path))
            {
                return path;
            }

            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

    }
}
