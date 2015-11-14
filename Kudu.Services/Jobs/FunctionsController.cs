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
                return Request.CreateResponse(HttpStatusCode.Created);
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> List()
        {
            using (_tracer.Step("FunctionsController.list()"))
            {
                var configContents = await Task.WhenAll(
                    FileSystemHelpers
                    .GetDirectories(_environment.FunctionsPath)
                    .Select(d => Path.Combine(d, Constants.FunctionsConfigFile))
                    .Where(FileSystemHelpers.FileExists)
                    .Select(FileSystemHelpers.ReadAllTextFromFileAsync));
                var configList = configContents.Select(JsonConvert.DeserializeObject);

                return Request.CreateResponse(HttpStatusCode.OK, configList);
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> Get(string name)
        {
            using (_tracer.Step($"FunctionsController.Get({name})"))
            {
                var config = await GetFunctionConfig(name);
                return Request.CreateResponse(HttpStatusCode.OK, config);
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

        [HttpGet]
        public HttpResponseMessage GetScript(string name)
        {
            using (_tracer.Step($"FunctionsController.GetScript({name})"))
            {
                var path = GetFunctionPath(name);
                var script = Path.Combine(path, Constants.FunctionsScriptFile);
                if (FileSystemHelpers.FileExists(script))
                {
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    var fileStream = new FileStream(script, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, useAsync: true);
                    response.Content = new StreamContent(fileStream, BufferSize);
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");
                    return response;
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> PutScript(string name)
        {
            using (_tracer.Step($"FunctionsController.PutScript({name})"))
            {
                var path = GetFunctionPath(name);
                var scriptPath = Path.Combine(path, Constants.FunctionsScriptFile);
                var script = await Request.Content.ReadAsStringAsync();
                await FileSystemHelpers.WriteAllTextToFileAsync(scriptPath, script);
                return Request.CreateResponse(HttpStatusCode.Created);
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

        public string GetFunctionPath(string name)
        {
            var path = Path.Combine(_environment.FunctionsPath, name);
            if (FileSystemHelpers.DirectoryExists(path))
            {
                return path;
            }

            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        public async Task<object> GetFunctionConfig(string name)
        {
            var path = Path.Combine(GetFunctionPath(name), Constants.FunctionsConfigFile);
            if (FileSystemHelpers.FileExists(path))
            {
                return JsonConvert.DeserializeObject(await FileSystemHelpers.ReadAllTextFromFileAsync(path));
            }

            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        public Task<string> GetFunctionScript(string name)
        {
            var path = GetFunctionPath(name);
            var script = Path.Combine(path, Constants.FunctionsScriptFile);
            if (FileSystemHelpers.FileExists(script))
            {
                return FileSystemHelpers.ReadAllTextFromFileAsync(script);
            }

            throw new HttpResponseException(HttpStatusCode.NotFound);
        }
    }
}
