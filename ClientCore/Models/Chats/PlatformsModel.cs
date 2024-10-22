using DeltaModel;
using MongoDB.Bson.Serialization.Serializers;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientCore.Support;
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
        private readonly IAppEnvironment _appEnvironment;

        public ObservableCollection<ChatToAdd> ChatsToAdd { get; } = new ObservableCollection<ChatToAdd>();

        public ObservableCollection<ChatModel> Chats { get; } = new ObservableCollection<ChatModel>();

        public Property<bool> ChatsEnabled { get; } = new Property<bool>();

        public Property<bool> IsChatsOpened { get; } = new Property<bool>();

        public Property<bool> HasSupportedChannels { get; } = new Property<bool>();

        public Property<double> FontSize { get; } = new Property<double>(16);

        public Property<double> FontSizeAuthor { get; } = new Property<double>(12);

        public string SupportedChannels { get; set; }

        public IPlatforms Core => _coreData.Root.Platforms;

        public PlatformsModel(CoreData coreData, StaticFilesCacheService staticFilesCacheService, HubConnectionService hubConnectionService,
            IAppEnvironment appEnvironment)
        {
            _coreData = coreData;
            _staticFilesCacheService = staticFilesCacheService;
            _hubConnectionService = hubConnectionService;
            _appEnvironment = appEnvironment;
        }

        internal void Start()
        {
            _coreData.Subscriptions.SubscribeForType<IChannel>((s, c) => Update());
            _coreData.Subscriptions.SubscribeForType<IChat>((s, c) => Update());
            _coreData.Subscriptions.SubscribeForProperties<IChannel>(c => c.TargetMode, (s, c, p) => Update());

            _coreData.Subscriptions.SubscribeForAnyProperty<IChat>((chat, a, b, c) => UpdateChatModel(chat));

            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(c => c.ChatsFontSize, (s, c, p) => Update());

            var supportedTargets = Core.PlatformInfos.Where(s => (s.Flags & PlatformInfoFlags.Chats) > 0).Select(s => _coreData.Root.Targets[s.TargetId].Name).OrderBy(s => s);
            SupportedChannels = $"You don't have configured targets yet for which we support chats. The targets where we support them are: " + 
                                    string.Join(", ", supportedTargets) + ". We are working hard to extend this list.";

            FontSize.Value = _coreData.ThisDevice.DeviceSettings.ChatsFontSize + 16;
            FontSizeAuthor.Value = FontSize.Value * 12 / 16;
            FontSize.OnChange = (o, b) =>
            {
                var val = Math.Round(b - 16);
                if (_coreData.ThisDevice.DeviceSettings.ChatsFontSize != val)
                    _coreData.ThisDevice.DeviceSettings.ChatsFontSize = val;

                FontSizeAuthor.Value = FontSize.Value * 12 / 16;
            };
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

            ChatsEnabled.Value = HasSupportedChannels.Value;// _coreData.Root.Channels.Values.Any(s => s.TargetMode == TargetMode.AutoLogin);
            if (!ChatsEnabled.Value)
                IsChatsOpened.Value = false;

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

            chat.Reconnect = async () =>
            {
                if (remote.State == ChatState.Connected)
                {
                    remote.State = ChatState.Initializing;
                    await Task.Delay(500);
                }
                remote.State = ChatState.Connected;
            };
            chat.AuthenticateModel = GetAuthenticateModel(chat.TargetId);
            chat.SendMessage = () =>
            {
                if (!string.IsNullOrEmpty(chat.Message.Value))
                {
                    TaskHelper.RunUnawaited(() => _hubConnectionService.InvokeAsync(nameof(IConnectionHubServer.SendChatMessage), new ChatMessageToSend { ChatId = id, Msg = chat.Message.Value, AuthorId = chat.SendMessageToAuthorId }), "SendChatMessage");
                    chat.Message.Value = "";
                }
            };
            chat.ResetSendMessageTo = () =>
            {
                chat.SendMessageTo.Value = null;
                chat.SendMessageToAuthorId = null;
            };

            _ = LoadLogo(chat);

            UpdateChatModel(chat, remote);
            return chat;
        }

        private void RefilterMessages(ChatModel chat, string toAddress)
        {
            chat.FilteredMessages.Clear();
            if (toAddress != null)
            {
                var filter = GetFilter(toAddress);
                chat.Messages.Where(s => filter(chat, s)).ToList().ForEach(s => chat.FilteredMessages.Add(s));
                chat.FilteredMessagesScroll.Value = AutoScollingStatusType.AutoScrollingToBottom;
            }
        }
           

        Func<ChatModel, ClientChatMessage, bool> GetFilter(string privateAuthor) => (chat, msg) =>
        {
            return privateAuthor != null && (msg.AuthorId == privateAuthor || msg.ToAuthorId == privateAuthor && msg.Self == true);
        };

        internal void WebBrowserModelAuthenticated(Dictionary<string, string> result, IPlatform platform)
        {
            platform.BrowserTokenAuthentication = new BrowserTokenAuthentication(result);
        }

        public BaseAuthenticateModel GetAuthenticateModel(string targetId)
        {
            if (targetId != null && Core.Platforms.TryGetValue(targetId, out var platform))
            {
                if (platform.AuthenticationType == PlatformAuthenticationType.OAuthServer)
                {
                    return new BaseAuthenticateModel
                    {
                        ShowPopupDialog = false,
                        Login = () =>
                        {
                            if (platform.AuthenticationData != null)
                            {
                                var url = platform.AuthenticationData.OAuthAuthenticationUrl.Replace("&", "^&");
                                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                            }
                            else
                                Log.Error($"platform.AuthenticationData == null for {targetId}");
                        },
                        Logout = () => { platform.ResetCounter += 1; }
                    };
                }
                else if (platform.AuthenticationType == PlatformAuthenticationType.UserPassword)
                {

                    _coreData.Root.Targets.TryGetValue(targetId, out var target);

                    var model = new UserPasswordAuthenticateModel
                    {
                        ShowPopupDialog = true,
                        Title = $"Enter credentials for '{target?.Name}'",
                        Description = $"Use credentials that you use to login to '{target?.Name}'. We are not storing them anywhere unless use explicitely select 'Remember credentials'.",
                        Logout = () => { platform.ResetCounter += 1; }

                    };

                    model.Login = () =>
                    {
                        platform.UserPasswordAuthentication = new UserPasswordAuthentication(model.User, model.Password, model.CanSave);
                    };
                    return model;
                }
                else if (platform.AuthenticationType == PlatformAuthenticationType.WebBrowser &&
                    platform.StaticAuthenticationData?.WebBrowserLoginSearch != null)
                {
                    _coreData.Root.Targets.TryGetValue(targetId, out var target);

                    var model = new WebBrowserAuthenticateModel(_appEnvironment, this, platform)
                    {
                        LoginSearch = platform.StaticAuthenticationData.WebBrowserLoginSearch,
                        ShowPopupDialog = true,
                        Title = $"Enter credentials for '{target?.Name}'",
                        Description = $"Use credentials that you use to login to '{target?.Name}'. Streamster does not store or handle your password anywhere.",
                        Logout = () => { platform.ResetCounter += 1; }
                    };
                    return model;
                }
            }

            return new UserPasswordAuthenticateModel(); // some default for binding
        }

        private void UpdateChatModel(ChatModel local, IChat remote)
        {
            local.State.Value = remote.State;
            local.StateDetails.Value = remote.StateDetails;
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

                ClientChatAuthor toAuthor = null;
                if (m.ToAuthorId != null)
                    chat.Authors.TryGetValue(m.ToAuthorId, out toAuthor);

                if (author != null)
                {
                    var authorName = "?";
                    bool self = author.Self;
                    if (self)
                        authorName = m.Private ? "private @ " + toAuthor?.Name : null; 
                    else
                        authorName = (author.Name ?? "") + (m.Private ? " @ private" : "");

                    bool first = lastMessage == null ? true : (lastMessage.AuthorId != m.AuthorId || lastMessage.Author != authorName);
                    bool showAuthor = authorName != null && first;

                    var msg = new ClientChatMessage
                    {
                        Message = m.Msg,
                        Author = authorName,
                        AuthorId = m.AuthorId,
                        ShowAuthor = showAuthor,
                        ToAuthorId = m.ToAuthorId,
                        Self = self,
                        First = first,
                        Type = (ChatMessageToReceiveType)m.Type,
                        Private = m.Private,
                        Platforms = this,
                        SelectUser = () =>
                        {
                            string newSendMessageToAuthorId = null;
                            string newSendMessageToAuthor = null;
                            if (!m.Internal)
                            {
                                if (self && m.Private)
                                {
                                    newSendMessageToAuthorId = m.ToAuthorId;
                                    newSendMessageToAuthor = toAuthor?.Name;
                                }
                                else if (!self)
                                {
                                    newSendMessageToAuthorId = m.AuthorId;
                                    newSendMessageToAuthor = author.Name;
                                }
                            }

                            if (chat.SendMessageToAuthorId != newSendMessageToAuthorId)
                            {
                                RefilterMessages(chat, newSendMessageToAuthorId);
                                chat.SendMessageToAuthorId = newSendMessageToAuthorId;
                            }
                            if (chat.SendMessageTo.Value != newSendMessageToAuthor)
                                chat.SendMessageTo.Value = newSendMessageToAuthor;
                        }
                    };
                    if (GetFilter(chat.SendMessageToAuthorId)(chat, msg))
                        chat.FilteredMessages.Add(msg);
                    chat.Messages.Add(msg);
                }
                else
                    Log.Warning($"Author with id '{m.AuthorId}' not found in chat {chat.TargetId}");
            }
        }
    }

    public class ChatModel
    {
        public string Id { get; set; }

        public string TargetId { get; set; }

        public string Title { get; set; }

        public Action Close { get; internal set; }

        public Action Reconnect { get; internal set; }

        public Property<byte[]> Logo { get; } = new Property<byte[]>();

        public Property<ChatState> State { get; } = new Property<ChatState>();

        public Property<string> AuthenticateMessage { get; set; }

        public BaseAuthenticateModel AuthenticateModel { get; set; }

        public Action SendMessage { get; set; }

        public Property<string> Message { get; } = new Property<string>();

        public ObservableCollection<ClientChatMessage> Messages { get; } = new ObservableCollection<ClientChatMessage>();

        public ObservableCollection<ClientChatMessage> FilteredMessages { get; } = new ObservableCollection<ClientChatMessage>();

        public Dictionary<string, ClientChatAuthor> Authors { get; } = new Dictionary<string, ClientChatAuthor>();

        public Property<string> StateDetails { get; } = new Property<string>();

        public Property<string> SendMessageTo { get; } = new Property<string> { };

        public Action ResetSendMessageTo { get; set; }

        public string SendMessageToAuthorId { get; set; }

        public Property<AutoScollingStatusType> FilteredMessagesScroll { get; } = new Property<AutoScollingStatusType>(AutoScollingStatusType.AutoScrollingToBottom);

        public Property<AutoScollingStatusType> MessagesScroll { get; } = new Property<AutoScollingStatusType>(AutoScollingStatusType.AutoScrollingToBottom);
    }

    public class ClientChatMessage
    {
        public string Message { get; set; }

        public bool Self { get; set; }

        public string Author { get; set; }

        public string AuthorId { get; set; }

        public bool ShowAuthor { get; set; }

        public bool First { get; set; }

        public ChatMessageToReceiveType Type { get; set; }

        public bool Private { get; set; }

        public string ToAuthorId { get; set; }

        public Action SelectUser { get; set; }

        public PlatformsModel Platforms { get; set; }
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