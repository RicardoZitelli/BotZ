using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using YoutubeExplode;
using NAudio.Wave;
using Discord.Audio;
using NAudio.Wave.SampleProviders;
using Discord.Commands;

namespace DiscordBotExample
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        public async Task MainAsync()
        {
            Console.WriteLine("Starting Bot");
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                MessageCacheSize = 1000,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates

            });

            _client.Log += Log;

            Console.WriteLine("Creating MessageReceived!");

            _client.MessageReceived += MessageReceived;
            Console.WriteLine("Created MessageReceived!");
            _client.Ready += () =>
            {
                Console.WriteLine("Bot is connected!");
                return Task.CompletedTask;
            };

            await _client.LoginAsync(TokenType.Bot, "MTA3NDc3MjM5NDQ3Mzk2MzUzMQ.GtOoIY.OcvrX2RYj9YcFuUdBLZ30KR_5_d8H2zPOOIZgU");
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task MessageReceived(SocketMessage message)
        {
            Console.WriteLine(message.Author + ": " + message.Content);
            if (message.Content.StartsWith("!play"))
            {
                var audioClient = await JoinVoiceChannel(message.Author as IVoiceState, message);

                if (audioClient == null)
                    await SendMessage(message.Channel, "Você precisa entrar em uma sala primeiro");
                else
                {
                    string url = message.Content.Split(" ")[1];

                    var youtube = new YoutubeClient();
                    var video = await youtube.Videos.GetAsync(url);
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                    var streamInfo = streamManifest.GetAudioOnlyStreams();

                    var filePath = Path.Combine(Path.GetTempPath(), $"{video.Id}.mp3");
                    var fileBytes = await youtube.Videos.Streams.GetAsync(streamInfo.AsEnumerable().First());

                    await File.WriteAllBytesAsync(filePath, ConvertStreamToByteArray(fileBytes));
                    
                    var output = audioClient.CreatePCMStream(AudioApplication.Music);

                    var waveReader = new WaveFileReader(filePath);
                    var volumeStream = new VolumeSampleProvider(waveReader.ToSampleProvider());
                    volumeStream.Volume = 0.5f;

                    var waveToFloat = new WaveToSampleProvider(volumeStream.ToWaveProvider());
                    var resampler = new WdlResamplingSampleProvider(waveToFloat, 48000);

                    await output.WriteAsync(ConvertWdlResamplingSampleProviderToByteArray(resampler));
                    output.Flush();
                    //await audioClient.DisconnectAsync();
                }

            }
        }
        
        [Command("JoinVoiceChannel", RunMode = RunMode.Async)]
        private async Task<IAudioClient?> JoinVoiceChannel(IVoiceState voiceState, SocketMessage message)
        {
            var channel = voiceState.VoiceChannel;
            if (channel != null)
            {   
                var connection = await channel.ConnectAsync();
                
                return connection;
            }
            else
            {
                await SendMessage(message.Channel, "Você precisa entrar em uma sala primeiro");
                return null;
            }
        }

        private async Task SendMessage(ISocketMessageChannel channel, string message)
        {
            await channel.SendMessageAsync(message);
        }

        private static byte[] ConvertStreamToByteArray(Stream stream)
        {
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        private static byte[] ConvertWdlResamplingSampleProviderToByteArray(WdlResamplingSampleProvider resamplingSampleProvider)
        {
            var buffer = new float[resamplingSampleProvider.WaveFormat.SampleRate * resamplingSampleProvider.WaveFormat.Channels];
            var memoryStream = new MemoryStream();
            var waveBuffer = new WaveBuffer(buffer.Length);
            int samplesRead;

            while ((samplesRead = resamplingSampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    waveBuffer.FloatBuffer[i] = buffer[i];
                }

                memoryStream.Write(waveBuffer.ByteBuffer, 0, samplesRead * 4);
            }

            return memoryStream.ToArray();
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}