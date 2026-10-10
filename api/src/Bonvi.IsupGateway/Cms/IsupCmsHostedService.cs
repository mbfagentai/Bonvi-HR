namespace Bonvi.IsupGateway.Cms;

/// <summary>
/// CMS serverini agentlardan OLDIN ishga tushiradi (xizmatlar ro'yxatdagi tartibda boshlanadi).
/// </summary>
/// <remarks>
/// SDK yuklanmasa yoki port band bo'lsa — istisno, ya'ni shlyuz ishga
/// tushmaydi va konteyner xato bilan to'xtaydi. Jim «ishlayapti, lekin
/// hech kim ulana olmaydi» holati bo'lmasin.
/// </remarks>
internal sealed class IsupCmsHostedService(IsupCmsServer server) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
