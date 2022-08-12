using Grpc.Core;
using GrpcService.Server;
using System.Text.Json;
using GrpcService.Server.Contracts;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace GrpcService.Server.Services
{
    public class GitHubService:GitHubCallService.GitHubCallServiceBase
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<GitHubService> _logger;
        public GitHubService(IHttpClientFactory factory, ILogger<GitHubService> logger)
        {
            _clientFactory = factory;
            _logger = logger;
        }

        public override async Task<GitHubResponse> GetUserDetails(GithubUserRequest request,ServerCallContext context)
        {
            var factory = _clientFactory.CreateClient();
            factory.DefaultRequestHeaders.Add("User-Agent", "gRPC");
            var data = await GetDetails(request, factory,context);
            return new GitHubResponse {
                Id = data!.Id,
                Name = data.Name,
                Bio = data.Bio,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                UserName = request.Name
            };
        }

        public override async Task Chat(IAsyncStreamReader<Msg> requestStream, IServerStreamWriter<Msg> responseStream, ServerCallContext context)
        {

            var task1 = ClientToServer(requestStream, context);
            var task2 = ServerToClient(responseStream, context);

            await Task.WhenAll(task1, task2);

        }

        public async Task ClientToServer(IAsyncStreamReader<Msg> requestStream,ServerCallContext context)
        {
            while (await requestStream.MoveNext() && !context.CancellationToken.IsCancellationRequested)
            {
                var data = requestStream.Current;
                string text = $"user sends the message {data.MessageText} and user was {data.SentBy}";
                _logger.LogInformation(text);
            }
        }

        public async Task ServerToClient(IServerStreamWriter<Msg> outputStream, ServerCallContext context)
        {
            var counter = 0;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await outputStream.WriteAsync(new Msg { MessageText = $"Hi from user Yesus at {++counter} times", SentBy = "Yesu Rajan Jenifer Jagan" });
                await Task.Delay(2000);
            }
        }

        public override async Task<GitHubUserResponse> GetUserDetailsIdOnly(GithubUserRequest request, ServerCallContext context)
        {
            var factory = _clientFactory.CreateClient();
            factory.DefaultRequestHeaders.Add("User-Agent", "gRPC");
            var data = await GetDetails(request, factory,context);
            return new GitHubUserResponse
            {
                Id = data!.Id
            };
        }
        public override async Task<Empty> GetInputOnly(IAsyncStreamReader<GithubUserRequest> requestStream, ServerCallContext context)
        {
            var factory = _clientFactory.CreateClient();
            factory.DefaultRequestHeaders.UserAgent.ParseAdd("gRPC");
            //await foreach(var request in requestStream.ReadAllAsync(context.CancellationToken))
            //{
            //    var data = await GetDetails(request, factory, context);
            //    _logger.LogInformation($"user input string is {request.Name}");
            //}
            while(await requestStream.MoveNext() && !context.CancellationToken.IsCancellationRequested)
            {
                var data = await GetDetails(requestStream.Current, factory, context);
                _logger.LogInformation($"user input string is {data.Name}");
            }
            return new();
        }

        public override async Task<MultiGitHubResponse> GetUserRequestStream(IAsyncStreamReader<GithubUserRequest> requestStream,ServerCallContext context)
        {
            var factory = _clientFactory.CreateClient();
            factory.DefaultRequestHeaders.UserAgent.ParseAdd("gRPC");
            var returnResponse = new MultiGitHubResponse
            {
                Response = { }
            };
            try
            {
                await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    var data = await GetDetails(request, factory, context);
                    returnResponse.Response.Add(new GitHubResponse
                    {
                        Id = data!.Id,
                        Name = data.Name,
                        Bio = data.Bio,
                        Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                        UserName = request.Name
                    });
                }
            }
            catch(Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }
            return returnResponse;
        }


        public override async Task GetUserDetailsStream(GithubUserRequest request,IServerStreamWriter<GitHubResponse> writer,ServerCallContext context)
        {
            var factory = _clientFactory.CreateClient();
            factory.DefaultRequestHeaders.UserAgent.ParseAdd("gRPC");
            for(var i = 0;i<=30;i++)
            {
                if(context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("User cancelled the request");
                    break;
                }
                var data = await GetDetails(request, factory,context);
                await writer.WriteAsync(new GitHubResponse
                {
                    Id = data!.Id,
                    Name = data.Name,
                    Bio = data.Bio,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    UserName = request.Name
                });
                await Task.Delay(1000);
            }
        }

        public static async Task<GitHubResponseContract> GetDetails(GithubUserRequest request,HttpClient factory,ServerCallContext context)
        {
            
            string responseText = await factory.GetStringAsync($"https://api.github.com/users/{request.Name}");
            var data = JsonSerializer.Deserialize<GitHubResponseContract>(responseText);
            return data!;
        }
    }
}
