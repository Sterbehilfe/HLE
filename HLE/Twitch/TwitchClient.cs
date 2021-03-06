using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HLE.Collections;
using HLE.Twitch.Args;
using HLE.Twitch.Models;

namespace HLE.Twitch;

/// <summary>
/// A class that represents a Twitch chat client. Uses <see cref="IrcClient"/> to connect to the chat servers and <see cref="IrcHandler"/> to handle messages.
/// </summary>
public class TwitchClient
{
    /// <summary>
    /// The username of the client.
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// Indicates whether the client is connected anonymously or not.
    /// </summary>
    public bool IsAnonymousLogin { get; }

    /// <summary>
    /// Indicates whether the client is connected or not.
    /// </summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// The list of channels the client is connected to. Channels can be retrieved by the owner's username or user id in order to read the room state, e.g. if slow-mode is on.
    /// </summary>
    public ChannelList Channels { get; } = new();

    #region Events

    /// <summary>
    /// Is invoked if the client connects.
    /// </summary>
    public event EventHandler? OnConnected;

    /// <summary>
    /// Is invoked if the client disconnects.
    /// </summary>
    public event EventHandler? OnDisconnected;

    /// <summary>
    /// Is invoked if a user joins a channel. A user can be the client or any other user.
    /// </summary>
    public event EventHandler<JoinedChannelArgs>? OnJoinedChannel;

    /// <summary>
    /// Is invoked if a user leaves a channel. A user can be the client or any other user.
    /// </summary>
    public event EventHandler<LeftChannelArgs>? OnLeftChannel;

    /// <summary>
    /// Is invoked if a room state has been received.
    /// </summary>
    public event EventHandler<RoomstateArgs>? OnRoomstateReceived;

    /// <summary>
    /// Is invoked if a chat message has been received.
    /// </summary>
    public event EventHandler<ChatMessage>? OnChatMessageReceived;

    /// <summary>
    /// Is invoked if the client sends a chat messages.
    /// </summary>
    public event EventHandler<ChatMessage>? OnChatMessageSent;

    /// <summary>
    /// Is invoked if data is received from the chat server.
    /// </summary>
    public event EventHandler<string>? OnDataReceived;

    /// <summary>
    /// Is invoked if data is sent to the chat server.
    /// </summary>
    public event EventHandler<string>? OnDataSent;

    #endregion Events

    private readonly IrcClient _client;
    private readonly IrcHandler _ircHandler = new();
    private readonly List<string> _ircChannels = new();

    private static readonly Regex _channelPattern = new(@"^#?\w{3,25}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// The constructor for an anonymous chat client. An anonymous chat client can only receive messages, but cannot send any messages.<br/>
    /// Connects with the username "justinfan123".
    /// </summary>
    public TwitchClient()
    {
        Username = "justinfan123";
        _client = new(Username, null);
        IsAnonymousLogin = true;

        SetEvents();
    }

    /// <summary>
    /// The constructor for a normal chat client.
    /// </summary>
    /// <param name="username">The username of the client</param>
    /// <param name="oAuthToken">The OAuth token of the client</param>
    /// <param name="isVerifiedBot">If true, the client uses different rate limiting to join channels faster. Condition to use this is that your Twitch account has to be a verified bot.</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if <paramref name="username"/> or <paramref name="oAuthToken"/> are in a wrong format.</exception>
    public TwitchClient(string username, string oAuthToken, bool isVerifiedBot = false)
    {
        Username = FormatUsername(username);
        oAuthToken = ValidateOAuthToken(oAuthToken);
        _client = new(Username, oAuthToken, isVerifiedBot);

        SetEvents();
    }

    private void SetEvents()
    {
        _client.OnConnected += (_, e) => OnConnected?.Invoke(this, e);
        _client.OnDisconnected += (_, e) => OnDisconnected?.Invoke(this, e);
        _client.OnDataReceived += IrcClient_OnDataReceived;
        _client.OnDataSent += IrcClient_OnDataSent;

        _ircHandler.OnJoinedChannel += (_, e) => OnJoinedChannel?.Invoke(this, e);
        _ircHandler.OnRoomstateReceived += IrcClient_OnRoomstateReceived;
        _ircHandler.OnRoomstateReceived += (_, e) => OnRoomstateReceived?.Invoke(this, e);
        _ircHandler.OnChatMessageReceived += IrcHandler_OnChatMessageReceived;
        _ircHandler.OnPingReceived += (_, e) => SendRaw($"PONG :{e.Message}");
        _ircHandler.OnLeftChannel += (_, e) => OnLeftChannel?.Invoke(this, e);
    }

    /// <summary>
    /// Sends a chat messages.
    /// </summary>
    /// <param name="channel">The username of the channel owner</param>
    /// <param name="message">The message that will be sent</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if <paramref name="channel"/> is in the wrong format.</exception>
    public void Send(string channel, string message)
    {
        if (!IsConnected || IsAnonymousLogin)
        {
            return;
        }

        channel = FormatUsername(channel);
        if (!_ircChannels.Contains(channel))
        {
            return;
        }

        _client.SendMessage(channel, message);
    }

    /// <summary>
    /// Sends a chat message.
    /// </summary>
    /// <param name="channelId">The user id of the channel owner</param>
    /// <param name="message">The message that will be sent</param>
    public void Send(long channelId, string message)
    {
        string? channel = Channels.FirstOrDefault(c => c.Id == channelId)?.Name;
        if (channel is null)
        {
            return;
        }

        Send($"#{channel}", message);
    }

    /// <summary>
    /// Sends a raw message to the chat server.
    /// </summary>
    /// <param name="message">The raw message</param>
    public void SendRaw(string message)
    {
        if (!IsConnected || IsAnonymousLogin)
        {
            return;
        }

        _client.SendRaw(message);
    }

    /// <summary>
    /// Connects the client to the chat server.
    /// </summary>
    public void Connect()
    {
        if (IsConnected)
        {
            return;
        }

        _client.Connect(_ircChannels);
    }

    /// <summary>
    /// If the client is not connected, adds the channel to the channel list, otherwise connects the client to the channel.
    /// </summary>
    /// <param name="channel">The channel</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if the <paramref name="channel"/> is in the wrong format.</exception>
    public void JoinChannel(string channel)
    {
        channel = FormatUsername(channel);
        _ircChannels.Add(channel);
        if (IsConnected)
        {
            _client.JoinChannel(channel);
        }
    }

    /// <summary>
    /// If the client is not connected, adds the channels to the channel list, otherwise connects the client to the channels.
    /// </summary>
    /// <param name="channels">The channels</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if any of <paramref name="channels"/> is in the wrong format.</exception>
    public void JoinChannels(params string[] channels)
    {
        channels = FormatUsernames(channels).ToArray();
        _ircChannels.AddRange(channels);
        if (IsConnected)
        {
            channels.ForEach(_client.JoinChannel);
        }
    }

    /// <summary>
    /// If the client is not connected, adds the channels to the channel list, otherwise connects the client to the channels.
    /// </summary>
    /// <param name="channels">The channels</param>
    /// // <exception cref="FormatException">Throws a <see cref="FormatException"/> if any of <paramref name="channels"/> is in the wrong format.</exception>
    public void JoinChannels(IEnumerable<string> channels)
    {
        string[] chnls = FormatUsernames(channels).ToArray();
        _ircChannels.AddRange(chnls);
        if (IsConnected)
        {
            chnls.ForEach(_client.JoinChannel);
        }
    }

    /// <summary>
    /// If the client is not connected, removes the channel from the channel list, otherwise leaves the channel.
    /// </summary>
    /// <param name="channel">The channel</param>
    public void LeaveChannel(string channel)
    {
        if (_ircChannels.Count == 0)
        {
            return;
        }

        channel = FormatUsername(channel);
        _ircChannels.Remove(channel);
        Channels.Remove(channel[1..]);
        if (IsConnected)
        {
            _client.LeaveChannel(channel);
        }
    }

    /// <summary>
    /// If the client is not connected, removes the channels from the channel list, otherwise leaves the channels.
    /// </summary>
    /// <param name="channels">The channels</param>
    public void LeaveChannels(params string[] channels)
    {
        foreach (string channel in channels)
        {
            LeaveChannel(channel);
        }
    }

    /// <summary>
    /// If the client is not connected, clears the channel list, otherwise leaves all channels.
    /// </summary>
    public void LeaveChannels()
    {
        if (_ircChannels.Count == 0)
        {
            return;
        }

        if (IsConnected)
        {
            foreach (string channel in _ircChannels)
            {
                LeaveChannel(channel);
            }
        }

        Channels.Clear();
        _ircChannels.Clear();
    }

    /// <summary>
    /// Disconnects the client from the chat server.
    /// </summary>
    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        _client.Disconnect();
        OnDisconnected?.Invoke(this, EventArgs.Empty);
    }

    private void IrcClient_OnDataReceived(object? sender, Memory<byte> e)
    {
        string message = Encoding.UTF8.GetString(e.ToArray());
        string[] lines = message.Remove("\r").Split('\n');
        lines.ForEach(l =>
        {
            _ircHandler.Handle(l);
            OnDataReceived?.Invoke(this, l);
        });
    }

    private void IrcClient_OnDataSent(object? sender, Memory<byte> e)
    {
        string message = Encoding.UTF8.GetString(e.ToArray());
        OnDataSent?.Invoke(this, message);
    }

    private void IrcClient_OnRoomstateReceived(object? sender, RoomstateArgs e)
    {
        Channels.Update(e);
    }

    private void IrcHandler_OnChatMessageReceived(object? sender, ChatMessage e)
    {
        if (e.Username == Username)
        {
            OnChatMessageSent?.Invoke(this, e);
        }
        else
        {
            OnChatMessageReceived?.Invoke(this, e);
        }
    }

    private static string FormatUsername(string username)
    {
        if (!_channelPattern.IsMatch(username))
        {
            throw new FormatException("The channel or username is in an invalid format.");
        }

        return (username.StartsWith('#') ? username : $"#{username}").ToLower();
    }

    private static IEnumerable<string> FormatUsernames(IEnumerable<string> usernames)
    {
        return usernames.Select(FormatUsername);
    }

    private static string ValidateOAuthToken(string oAuthToken)
    {
        Regex oAuthPattern = new(@"^oauth:[a-z0-9]{30}$", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        Regex oAuthPatternNoPrefix = new(@"^[a-z0-9]{30}$", RegexOptions.None, TimeSpan.FromMilliseconds(250));

        if (oAuthPattern.IsMatch(oAuthToken))
        {
            return oAuthToken;
        }

        if (oAuthPatternNoPrefix.IsMatch(oAuthToken))
        {
            return $"oauth:{oAuthToken}";
        }

        throw new FormatException("The OAuthToken is in an invalid format.");
    }
}
