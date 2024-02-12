using MailKit.Net.Imap;

namespace Net.Leksi.MicroService.ImapReader;

internal class Client: ImapClient
{
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        await base.ConnectAsync(host, port, useSsl: false, cancellationToken);
    }
}
