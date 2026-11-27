namespace Transfers.Application;

public interface IOutbox
{
    void Add(string type, object @event, Guid messageId);   // tylko dodaje do DbContext, bez SaveChanges
}
