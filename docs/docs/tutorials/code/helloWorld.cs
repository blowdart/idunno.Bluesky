using idunno.Bluesky;

using BlueskyAgent agent = new();
await agent.Login(handle, password);
var postResult = await agent.Post("Hello World");

if (postResult.Succeeded)
{
    var postUri = postResult.Result.Uri;
    var postCid = postResult.Result.Cid;
}
