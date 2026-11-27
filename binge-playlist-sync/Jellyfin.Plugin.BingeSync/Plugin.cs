using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.BingeSync;

public sealed class Plugin(IApplicationPaths paths, IXmlSerializer serializer)
    : BasePlugin<BasePluginConfiguration>(paths, serializer)
{
    public override string Name => "Media Harbour Plugin";
    public override string Description => "Syncs Binge playlists, playlist progress, IPTV Favourites and Recently Watched for each Jellyfin user.";
    public override Guid Id => Guid.Parse("ec199d02-59bc-4ad2-89b4-38011fa74983");
}

public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddSingleton<PlaylistStore>();
        services.AddSingleton<IptvStore>();
    }
}
