using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using HLE.Twitch.Attributes;

namespace HLE.Twitch.Args;

/// <summary>
/// <see cref="EventArgs"/> used when the state of a chat room changed.
/// For example if emote-only mode has been turned on.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members")]
public class RoomstateArgs : EventArgs
{
    /// <summary>
    /// Indicates whether emote-only mode is turned on or off.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.EmoteOnly))]
    [IrcTagName("emote-only")]
    public bool EmoteOnly { get; init; }

    /// <summary>
    /// Indicates whether followers-only mode is turned on or off.
    /// Value is "-1" if turned off, otherwise the value indicates the number of minutes a user has to follow the channel in order to be able to send messages.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.FollowersOnly))]
    [IrcTagName("followers-only")]
    public int FollowersOnly { get; init; }

    /// <summary>
    /// Indicates whether R9K mode is turned on or off.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.R9K))]
    [IrcTagName("r9k")]
    public bool R9K { get; init; }

    /// <summary>
    /// The user id of the channel owner.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.Id))]
    [IrcTagName("room-id")]
    public long ChannelId { get; init; }

    /// <summary>
    /// The username of the channel owner.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.Name))]
    public string Channel { get; }

    /// <summary>
    /// Indicates whether slow mode os turned on or off.
    /// Value is "0" if turned off, otherwise the value indicates the number of seconds between each message a user can send.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.SlowMode))]
    [IrcTagName("slow")]
    public int SlowMode { get; init; }

    /// <summary>
    /// Indicates whether subs-only mode is turned on or off.
    /// </summary>
    [ChannelPropName(nameof(Models.Channel.SubsOnly))]
    [IrcTagName("subs-only")]
    public bool SubsOnly { get; init; }

    internal List<PropertyInfo> ChangedProperties { get; } = new();

    internal static PropertyInfo[] IrcProps { get; } = typeof(RoomstateArgs).GetProperties().Where(p => p.GetCustomAttribute<IrcTagName>() is not null).ToArray();

    private static readonly MethodInfo[] _ircMethods = typeof(RoomstateArgs).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m.GetCustomAttribute<MsgPropName>() is not null)
        .ToArray();

    /// <summary>
    /// The basic constructor of <see cref="RoomstateArgs"/>.
    /// </summary>
    /// <param name="ircMessage">The IRC message.</param>
    /// <param name="split">The IRC message split on whitespaces. Optional if a split has been done prior to calling this method.</param>
    public RoomstateArgs(string ircMessage, string[]? split = null)
    {
        split ??= ircMessage.Split();
        string[] roomstateSplit = split[0][1..].Split(';').ToArray();
        Dictionary<string, string> tagDic = roomstateSplit.Select(s => s.Split('=')).ToDictionary(sp => sp[0], sp => sp[1]);

        foreach (PropertyInfo prop in IrcProps)
        {
            IrcTagName attr = prop.GetCustomAttribute<IrcTagName>()!;
            if (!tagDic.TryGetValue(attr.Value, out string? value))
            {
                continue;
            }

            MethodInfo method = _ircMethods.FirstOrDefault(m => m.GetCustomAttribute<MsgPropName>()!.Value == prop.Name)!;
            object? result = method.Invoke(this, new object[]
            {
                value
            });

            if (result is null)
            {
                continue;
            }

            prop.SetValue(this, result);
            ChangedProperties.Add(prop);
        }

        Channel = split[^1][1..];
    }

    [MsgPropName(nameof(EmoteOnly))]
    private bool GetEmoteOnly(string value) => value[^1] == '1';

    [MsgPropName(nameof(FollowersOnly))]
    private int GetFollowersOnly(string value) => int.Parse(value);

    [MsgPropName(nameof(R9K))]
    private bool GetR9K(string value) => value[^1] == '1';

    [MsgPropName(nameof(ChannelId))]
    private long GetChannelId(string value) => long.Parse(value);

    [MsgPropName(nameof(SlowMode))]
    private int GetSlowMode(string value) => int.Parse(value);

    [MsgPropName(nameof(SubsOnly))]
    private bool GetSubsOnly(string value) => value[^1] == '1';
}
