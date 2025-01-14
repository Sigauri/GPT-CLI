﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using GPT.CLI.Embeddings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using OpenAI.GPT3.Extensions;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using GPT.CLI.Chat.Discord;

namespace GPT.CLI;

class Program
{
    static async Task Main(string[] args)
    {
        // Define command line parameters
        var apiKeyOption = new Option<string>("--api-key", "Your OpenAI API key") ;
        var baseUrlOption = new Option<string>("--base-domain", "The base URL for the OpenAI API");
        var promptOption = new Option<string>("--prompt", "The prompt for text generation. Optional for most commands.") {IsRequired = true};

        var configOption = new Option<string>("--config", () => "appSettings.json", "The path to the appSettings.json config file");

        // Add the rest of the available fields as command line parameters
        var modelOption = new Option<string>("--model", () => Models.ChatGpt3_5Turbo, "The model ID to use.");
        var maxTokensOption = new Option<int>("--max-tokens", () => 1000, "The maximum number of tokens to generate in the completion.");
        var temperatureOption = new Option<double>("--temperature", "The sampling temperature to use, between 0 and 2");
        var topPOption = new Option<double>("--top-p", "The value for nucleus sampling");
        var nOption = new Option<int>("--n", () => 1, "The number of completions to generate for each prompt.");
        var streamOption = new Option<bool>("--stream", () => true, "Whether to stream back partial progress");
        var stopOption = new Option<string>("--stop", "Up to 4 sequences where the API will stop generating further tokens");
        var presencePenaltyOption = new Option<double>("--presence-penalty", "Penalty for new tokens based on their presence in the text so far");
        var frequencyPenaltyOption = new Option<double>("--frequency-penalty", "Penalty for new tokens based on their frequency in the text so far");
        var logitBiasOption = new Option<string>("--logit-bias", "Modify the likelihood of specified tokens appearing in the completion");
        var userOption = new Option<string>("--user", "A unique identifier representing your end-user");
        var chatCommand = new Command("chat", "Starts listening in chat mode.");
        var embedCommand = new Command("embed", "Create an embedding for data redirected via STDIN.");
        var httpCommand = new Command("http", "Starts an HTTP server to listen for requests.");
        var discordCommand = new Command("discord", "Starts the CLI as a Discord bot that receives messages from all channels on your server.");
        var botTokenOption = new Option<string>("--bot-token", "The token for your Discord bot.");
        var maxChatHistoryLengthOption = new Option<uint>("--max-chat-history-length", () => 1024, "The maximum message length to keep in chat history (chat & discord modes).");

        var chunkSizeOption = new Option<int>("--chunk-size", () => 1024,
            "The size to chunk down text into embeddable documents.");
        var embedFileOption = new Option<string[]>("--file", "Name of a file from which to load previously saved embeddings. Multiple files allowed.")
            { AllowMultipleArgumentsPerToken = true, Arity = ArgumentArity.OneOrMore};
        var embedDirectoryOption = new Option<string[]>("--directory",
            "Name of a directory from which to load previously saved embeddings. Multiple directories allowed.")
        {
            AllowMultipleArgumentsPerToken = true, Arity = ArgumentArity.OneOrMore
        };

        var httpPortOption = new Option<int>("--port", () => 5000, "The port to listen on for HTTP requests.");
        var sslPortOption = new Option<int>("--ssl-port", () => 5001, "The port to listen on for HTTPS requests.");

        var matchLimitOption = new Option<int>("--match-limit", () => 3,
            "Limits the number of embedding chunks to use when applying context.");

        embedCommand.AddValidator(result =>
        {
            if (!Console.IsInputRedirected)
            {
                result.ErrorMessage = "Input required for embedding";
            }
        });

        embedCommand.AddOption(chunkSizeOption);

        // Create a command and add the options
        var rootCommand = new RootCommand("GPT Console Application");

        rootCommand.AddGlobalOption(apiKeyOption);
        rootCommand.AddGlobalOption(baseUrlOption);
        rootCommand.AddOption(promptOption);
        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddGlobalOption(modelOption);
        rootCommand.AddGlobalOption(maxTokensOption);
        rootCommand.AddGlobalOption(temperatureOption);
        rootCommand.AddGlobalOption(topPOption);
        rootCommand.AddOption(nOption);
        rootCommand.AddGlobalOption(streamOption);
        rootCommand.AddGlobalOption(stopOption);
        rootCommand.AddGlobalOption(presencePenaltyOption);
        rootCommand.AddGlobalOption(frequencyPenaltyOption);
        rootCommand.AddGlobalOption(logitBiasOption);
        rootCommand.AddGlobalOption(userOption);
        rootCommand.AddGlobalOption(embedFileOption);
        rootCommand.AddGlobalOption(embedDirectoryOption);

        rootCommand.AddOption(matchLimitOption);

        rootCommand.AddCommand(chatCommand);
        rootCommand.AddCommand(embedCommand);
        // We'll add this later when I have an api idea.
        //rootCommand.AddCommand(httpCommand);
        rootCommand.AddCommand(discordCommand);
        rootCommand.AddOption(botTokenOption);
        chatCommand.AddOption(maxChatHistoryLengthOption);
        discordCommand.AddOption(maxChatHistoryLengthOption);
        

        var binder = new GPTParametersBinder(
            apiKeyOption, baseUrlOption, promptOption, configOption,
            modelOption, maxTokensOption, temperatureOption, topPOption,
            nOption, streamOption, stopOption,
            presencePenaltyOption, frequencyPenaltyOption, logitBiasOption, 
            userOption, embedFileOption, embedDirectoryOption, chunkSizeOption, matchLimitOption, botTokenOption, maxChatHistoryLengthOption);

        ParameterMapping.Mode mode = ParameterMapping.Mode.Completion;

        // Set the handler for the rootCommand
        rootCommand.SetHandler(_ => {}, binder);
        chatCommand.SetHandler(_ => mode = ParameterMapping.Mode.Chat, binder);
        embedCommand.SetHandler(_ => mode = ParameterMapping.Mode.Embed, binder);
        httpCommand.SetHandler(_ => mode = ParameterMapping.Mode.Http, binder);
        discordCommand.SetHandler(_ => mode = ParameterMapping.Mode.Discord, binder);

        // Invoke the command
        var retValue = await new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .Build()
            .InvokeAsync(args);

        if (retValue == 0 && binder.GPTParameters != null)
        {
            // Set up dependency injection
            var services = new ServiceCollection(); 
            
            ConfigureServices(services, binder, mode);

            await using var serviceProvider = services.BuildServiceProvider();

            // get a OpenAILogic instance
            var openAILogic = serviceProvider.GetService<OpenAILogic>();

            switch (mode)
            {
                case ParameterMapping.Mode.Chat:
                {
                    await HandleChatMode(openAILogic, binder.GPTParameters);
                    break;
                }
                case ParameterMapping.Mode.Embed:
                {
                    await HandleEmbedMode(openAILogic, binder.GPTParameters);
                    break;
                }
                case ParameterMapping.Mode.Discord:
                {
                    await HandleDiscordMode(openAILogic, binder.GPTParameters, services);
                    break;
                }
                case ParameterMapping.Mode.Completion:
                default:
                {
                    await HandleCompletionMode(openAILogic, binder.GPTParameters);

                    break;
                }
            }
        }
    }

    private static async Task HandleDiscordMode(OpenAILogic openAILogic, GPTParameters gptParameters, IServiceCollection services)
    {
        var hostBuilder = new HostBuilder().ConfigureServices(innerServices =>
        {
            foreach (var service in services)
            {
                innerServices.Add(service);
            }

            innerServices.AddHostedService<DiscordBot>();
        });

        await hostBuilder.RunConsoleAsync();
    }

    private static async Task HandleEmbedMode(OpenAILogic openAILogic, GPTParameters gptParameters)
    {
        // Create and output embedding
        var documents = await Document.ChunkStreamToDocumentsAsync(Console.OpenStandardInput(), gptParameters.ChunkSize);

        await openAILogic.CreateEmbeddings(documents);

        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(documents));
    }

    private static async Task HandleCompletionMode(OpenAILogic openAILogic, GPTParameters gptParameters)
    {
        var chatRequest = Console.IsInputRedirected
            ? await ParameterMapping.MapChatEdit(gptParameters, openAILogic, Console.OpenStandardInput())
            : await ParameterMapping.MapChatCreate(gptParameters, openAILogic);

        var responses = gptParameters.Stream == true
            ? openAILogic.CreateChatCompletionAsyncEnumerable(chatRequest)
            : (await openAILogic.CreateChatCompletionAsync(chatRequest)).ToAsyncEnumerable();

        await foreach (var response in responses)
        {
            await OutputChatResponse(response);
        }
    }

    private static async Task HandleChatMode(OpenAILogic openAILogic, GPTParameters gptParameters)
    {
        var initialRequest = await ParameterMapping.MapCommon(gptParameters, openAILogic, new ChatCompletionCreateRequest()
        {
            Messages = new List<ChatMessage>(50)
            {
                new(StaticValues.ChatMessageRoles.System,
                    "You are ChatGPT CLI, the helpful assistant, but you're running on a command line.")
            }
        }, ParameterMapping.Mode.Chat);



        await Console.Out.WriteLineAsync(@"
 #####                       #####  ######  #######     #####  #       ### 
#     # #    #   ##   ##### #     # #     #    #       #     # #        #  
#       #    #  #  #    #   #       #     #    #       #       #        #  
#       ###### #    #   #   #  #### ######     #       #       #        #  
#       #    # ######   #   #     # #          #       #       #        #  
#     # #    # #    #   #   #     # #          #       #     # #        #  
 #####  #    # #    #   #    #####  #          #        #####  ####### ###");
        var sb = new StringBuilder();
        var documents = await ReadEmbedFilesAsync(gptParameters);
        documents.AddRange(await ReadEmbedDirectoriesAsync(gptParameters));

        var prompts = new List<string>();
        var promptResponses = new List<string>();

        do
        {
            // Keeping track of all the prompts and responses means we can rebuild the chat message history
            // without including the old context every time, saving on tokens
            var chatGpt = new ChatGPTLogic(openAILogic, initialRequest);
            chatGpt.ClearMessages();

            await Console.Out.WriteAsync("\r\n? ");
            var chatInput = await Console.In.ReadLineAsync();

            if (!string.IsNullOrWhiteSpace(chatInput))
            {
                if ("exit".Equals(chatInput, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }


                for (int i = 0; i < prompts.Count; i++)
                {
                    chatGpt.AppendMessage(new(StaticValues.ChatMessageRoles.User, prompts[i]));
                    chatGpt.AppendMessage(new(StaticValues.ChatMessageRoles.Assistant, promptResponses[i]));
                }

                
                // If there's embedded context provided, inject it after the existing chat history, and before the new prompt
                if (documents.Count > 0)
                {
                    // Search for the closest few documents and add those if they aren't used yet
                    var closestDocuments =
                        Document.FindMostSimilarDocuments(documents, await openAILogic.GetEmbeddingForPrompt(chatInput), gptParameters.ClosestMatchLimit);
                    if (closestDocuments != null)
                    {
                        foreach (var closestDocument in closestDocuments)
                        {
                            chatGpt.AppendMessage(new(StaticValues.ChatMessageRoles.User, 
                                    $"Embedding context for the next prompt: {closestDocument.Text}"));
                        }
                    }
                }

                prompts.Add(chatInput);
                chatGpt.AppendMessage(new(StaticValues.ChatMessageRoles.User, chatInput));

                // Get the new response:
                var responses = chatGpt.SendMessages();
                sb.Clear();
                await foreach (var response in responses)
                {
                    if (await OutputChatResponse(response))
                    {
                        foreach (var choice in response.Choices)
                        {
                            sb.Append(choice.Message.Content);
                        }
                    }
                }

                // Store the streamed response for the chat history
                promptResponses.Add(sb.ToString());

                // Output the 
                await Console.Out.WriteLineAsync();
            }
        } while (true);
    }

    public static async Task<List<Document>> ReadEmbedFilesAsync(GPTParameters parameters)
    {
        List<Document> documents = new ();
        if (parameters.EmbedFilenames is { Length: > 0 })
        {
            foreach (var embedFile in parameters.EmbedFilenames)
            {
                await using var fileStream = File.OpenRead(embedFile);

                documents.AddRange(Document.LoadEmbeddings(fileStream));
            }
        }

        return documents;
    }

    public static async Task<List<Document>> ReadEmbedDirectoriesAsync(GPTParameters parameters)
    {
        List<Document> documents = new();

        if (parameters.EmbedDirectoryNames is { Length: > 0 })
        {
            foreach (var embedDirectory in parameters.EmbedDirectoryNames)
            {
                var files = Directory.EnumerateFiles(embedDirectory);
                foreach (var file in files)
                {
                    await using var fileStream = File.OpenRead(file);

                    documents.AddRange(Document.LoadEmbeddings(fileStream));
                }
            }
        }

        return documents;
    }

    private static async Task<bool> OutputChatResponse(ChatCompletionCreateResponse response)
    {
        if (response.Successful)
        {
            foreach (var choice in response.Choices)
            {
                await Console.Out.WriteAsync(choice.Message.Content);
            }
        }
        else
        {
            await Console.Error.WriteAsync(response.Error?.Message?.Trim());
        }

        return response.Successful;
    }


    private static void ConfigureServices(IServiceCollection services, GPTParametersBinder gptParametersBinder, ParameterMapping.Mode mode)
    {
        var gptParameters = gptParametersBinder.GPTParameters;

        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile(gptParameters.Config ?? "appSettings.json", optional: true, reloadOnChange: false)
            .Build();

        gptParameters.ApiKey ??= Configuration["OpenAI:api-key"];
        gptParameters.BaseDomain ??= Configuration["OpenAI:base-domain"];

        // Add the configuration object to the services
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddOpenAIService(settings =>
        {
            settings.ApiKey = gptParameters.ApiKey;
            settings.BaseDomain = gptParameters.BaseDomain;
        });
        services.AddSingleton<OpenAILogic>();
        services.AddSingleton(_ =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMembers | 
                                 GatewayIntents.MessageContent | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions | 
                                 GatewayIntents.GuildMessageReactions | GatewayIntents.GuildEmojis,
                MessageCacheSize = 100
            };

            return new DiscordSocketClient(config);
        });
        services.AddSingleton<DiscordBot>();
        services.AddSingleton(_ => gptParameters);



        if (mode == ParameterMapping.Mode.Http)
        {
            // Add OpenAPI/Swagger document generation
            //services.AddSwaggerGen(c =>
            //{
            //    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GPT-CLI API", Version = "v1" });

            //    // Set the comments path for the Swagger JSON and UI
            //    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            //    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            //    c.IncludeXmlComments(xmlPath);

            //    // Include custom API descriptions
            //    var apiDescriptionsFile = "ApiDescriptions.xml";
            //    var apiDescriptionsPath = Path.Combine(AppContext.BaseDirectory, apiDescriptionsFile);
            //    c.IncludeXmlComments(apiDescriptionsPath);
            //});

            // Add Azure AD B2C authentication
            // Add authentication with Azure AD B2C
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(options =>
                {
                    Configuration.Bind("AzureAdB2C", options);
                    options.TokenValidationParameters.NameClaimType = "name";
                },options => Configuration.Bind("AzureAdB2C", options));

            
            // Add minimal API
            services.AddEndpointsApiExplorer();
            services.AddRouting();

            // Add Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
            });
        }
    }

    public static IConfigurationRoot Configuration { get; set; }

    private static bool VerifySignature(string publicKey, string signature, string timestamp, string body)
    {
        byte[] publicKeyBytes = StringToByteArray(publicKey);
        byte[] signatureBytes = StringToByteArray(signature);
        byte[] timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        var pubKeyParam = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
        var verifier = SignerUtilities.GetSigner("Ed25519");

        verifier.Init(false, pubKeyParam);

        byte[] combinedBytes = new byte[timestampBytes.Length + bodyBytes.Length];
        Buffer.BlockCopy(timestampBytes, 0, combinedBytes, 0, timestampBytes.Length);
        Buffer.BlockCopy(bodyBytes, 0, combinedBytes, timestampBytes.Length, bodyBytes.Length);

        verifier.BlockUpdate(combinedBytes, 0, combinedBytes.Length);

        return verifier.VerifySignature(signatureBytes);

        
    }
    private static byte[] StringToByteArray(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }
    

    public class InteractionPayload
    {
        public string ApplicationId { get; set; }
        public string Id { get; set; }
        public string Token { get; set; }
        public int Type { get; set; }
        public InteractionUser User { get; set; }
        public int Version { get; set; }
    }

    public class InteractionUser
    {
        public string Avatar { get; set; }
        public object AvatarDecoration { get; set; }
        public string Discriminator { get; set; }
        public object DisplayName { get; set; }
        public object GlobalName { get; set; }
        public string Id { get; set; }
        public int PublicFlags { get; set; }
        public string Username { get; set; }
    }

}