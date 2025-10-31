using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Test {
    public async Task TestMethod(IChatClient client) {
        var messages = new List<ChatMessage>();
        // This will show what methods are available
        var response = await client.CompleteAsync(messages);
    }
}
