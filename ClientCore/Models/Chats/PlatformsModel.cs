using Clutch.DeltaModel;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models.Chats
{
    public class PlatformsModel
    {
        private CoreData _coreData;
        private readonly StaticFilesCacheService _staticFilesCacheService;
        private readonly HubConnectionService _hubConnectionService;

        public ObservableCollection<ChatToAdd> ChatsToAdd { get; } = new ObservableCollection<ChatToAdd>();

        public ObservableCollection<ChatModel> Chats { get; } = new ObservableCollection<ChatModel>();

        public bool ChatsEnabled { get; } = ClientConstants.ChatsEnabled;

        public Property<bool> IsChatsOpened { get; } = new Property<bool>();

        public Property<bool> HasSupportedChannels { get; } = new Property<bool>();

        public string SupportedChannels { get; set; }

        public IPlatforms Core => _coreData.Root.Platforms;

        public PlatformsModel(CoreData coreData, StaticFilesCacheService staticFilesCacheService, HubConnectionService hubConnectionService)
        {
            _coreData = coreData;
            _staticFilesCacheService = staticFilesCacheService;
            _hubConnectionService = hubConnectionService;
        }

        internal void Start()
        {
            _coreData.Subscriptions.SubscribeForType<IChannel>((s, c) => Update());
            _coreData.Subscriptions.SubscribeForType<IChat>((s, c) => Update());

            _coreData.Subscriptions.SubscribeForAnyProperty<IChat>((chat, a, b, c) => UpdateChatModel(chat));

            var supportedTargets = Core.PlatformInfos.Where(s => (s.Flags & PlatformInfoFlags.Chats) > 0).Select(s => _coreData.Root.Targets[s.TargetId].Name).OrderBy(s => s);
            SupportedChannels = $"You don't have configured targets yet for which we support chats. The targets where we support them are: " + 
                                    string.Join(", ", supportedTargets) + ". We are working hard to extend this list.";

            Update();
        }

        private void UpdateChatModel(IChat chat)
        {
            var id = _coreData.GetId(chat);
            var local = Chats.FirstOrDefault(s => s.Id == id);
            if (local != null)
                UpdateChatModel(local, chat);
        }

        private void Update()
        {
            var infos = Core.PlatformInfos;
            var supportedTargetIds = infos.Where(s => (s.Flags & PlatformInfoFlags.Chats) > 0).Select(s => s.TargetId).ToHashSet();
            var channelTargetIds = _coreData.Root.Channels.Values.Where(s => supportedTargetIds.Contains(s.TargetId)).Select(s => s.TargetId).Distinct().OrderBy(s => s).ToList();

            HasSupportedChannels.ValueWithComparison = channelTargetIds.Count > 0;

            ListHelper.UpdateCollection(_coreData, Core.Chats.Values.ToList(), Chats, p => p.Id, (s, id) => CreateChat(s, id));
            var targetsToAdd = channelTargetIds.Select(s => _coreData.Root.Targets[s]).Where(s => !Chats.Any(r => r.TargetId == s.Id)).ToList();
            ListHelper.UpdateCollectionNoId(targetsToAdd, ChatsToAdd, (t, s) => t.Id == s.TargetId, s => new ChatToAdd { TargetId = s.Id, Title = s.Name, Add = () => OnChatOpen(s.Id) } );
        }

        private ChatModel CreateChat(IChat remote, string id)
        {
            var title = _coreData.Root.Targets.TryGetValue(remote.TargetId, out var target) ? target.Name : "???";
            var chat = new ChatModel
            {
                Id = id,
                TargetId = remote.TargetId,
                Title = title,
                Close = () => _coreData.Root.Platforms.Chats.Remove(id)
            };

            chat.Authenticate = () => Authenticate(chat.TargetId);
            chat.SendMessage = () =>
            {
                if (!string.IsNullOrEmpty(chat.Message.Value))
                {
                    _ = _hubConnectionService.InvokeAsync(nameof(IConnectionHubServer.SendChatMessage), new ChatMessageToSend { ChatId = id, Msg = chat.Message.Value });
                    chat.Message.Value = "";
                }
            };

            _ = LoadLogo(chat);

            UpdateChatModel(chat, remote);
            return chat;
        }

        public void Authenticate(string targetId)
        {
            if (Core.Platforms.TryGetValue(targetId, out var platform) && platform.AuthenticationData != null)
            {
                if (platform.AuthenticationData.AuthenticationType == PlatformAuthenticationType.OAuthServer)
                {
                    var url = platform.AuthenticationData.OAuthAuthenticationUrl.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (platform.AuthenticationData.AuthenticationType == PlatformAuthenticationType.UserPassword)
                { 
                }
            }
        }

        private void UpdateChatModel(ChatModel local, IChat remote)
        {
            local.State.Value = remote.State;
        }

        private async Task LoadLogo(ChatModel model)
        {
            string url = $"{ClientConstants.LoadBalancerFiles_Targets}/{model.TargetId}.png";
            model.Logo.Value = await _staticFilesCacheService.GetFileAsync(url);
        }

        private void OnChatOpen(string targetId)
        {
            var chat = _coreData.Create<IChat>();
            chat.State = ChatState.Initializing;
            chat.TargetId = targetId;
            var chatId = IdGenerator.New();
            Core.Chats.Add(chatId, chat);
        }

        public void OnReceiveChatMessagesData(ReceiveChatMessagesData p)
        {
            foreach(var target in p.Targets)
            {
                var chat = Chats.FirstOrDefault(s => s.TargetId == target.TargetId);
                if (chat != null)
                {
                    AddChatMessages(chat, target);
                }
                else Log.Warning($"Chat messages received for removed chat '{target.TargetId}'");
            }
        }

        private void AddChatMessages(ChatModel chat, ReceiveChatMessagesPerTarget target)
        {
            foreach (var a in target.Authors)
            {
                chat.Authors[a.Id] = new ClientChatAuthor { Name = a.Name, Self = a.Self };
            }

            foreach (var m in target.Messages)
            {
                var lastMessage = chat.Messages.Count > 0 ? chat.Messages[chat.Messages.Count - 1] : null;

                chat.Authors.TryGetValue(m.AuthorId, out var author);

                chat.Messages.Add(new ClientChatMessage
                {
                    Message = m.Msg,
                    Author = author?.Name ?? "?",
                    AuthorId = m.AuthorId,
                    Self = author?.Self ?? false,
                    First = lastMessage == null ? true : lastMessage.AuthorId != m.AuthorId
                });
            }
        }
    }

    public class ChatModel
    {
        public string Id { get; set; }

        public string TargetId { get; set; }

        public string Title { get; set; }

        public Action Close { get; internal set; }

        public Property<byte[]> Logo { get; } = new Property<byte[]>();

        public Property<ChatState> State { get; } = new Property<ChatState>();

        public Property<string> AuthenticateMessage { get; set; }

        public Action Authenticate { get; set; }

        public Action SendMessage { get; set; }

        public Property<string> Message { get; } = new Property<string>();

        public ObservableCollection<ClientChatMessage> Messages { get; } = new ObservableCollection<ClientChatMessage>();

        public Dictionary<string, ClientChatAuthor> Authors { get; } = new Dictionary<string, ClientChatAuthor>();
    }

    public class ClientChatMessage
    {
        public string Message { get; set; }

        public bool Self { get; set; }

        public string Author { get; set; }

        public string AuthorId { get; set; }

        public bool First { get; set; }
    }

    public class ClientChatAuthor
    {
        public string Name { get; set; }

        public bool Self { get; set; }
    }

    public class ChatToAdd
    {
        public string TargetId { get; set; }

        public string Title { get; set; }

        public Action Add { get; set; } 
    }
}