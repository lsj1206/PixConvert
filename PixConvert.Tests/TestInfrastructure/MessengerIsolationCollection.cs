using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.Tests;

[CollectionDefinition("MessengerIsolation", DisableParallelization = true)]
public sealed class MessengerIsolationCollection
{
}

internal sealed class StatusRequestRecorder : IDisposable
{
    public List<AppStatus> Requests { get; } = [];

    public StatusRequestRecorder()
    {
        WeakReferenceMessenger.Default.Register<AppStatusRequestMessage>(
            this,
            static (recipient, message) =>
                ((StatusRequestRecorder)recipient).Requests.Add(message.NewStatus));
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
