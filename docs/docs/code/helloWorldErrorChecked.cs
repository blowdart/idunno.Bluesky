using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;

using BlueskyAgent agent = new();

AtProtoHttpResult<bool> loginResult = await agent.Login("handle", "password");

if (loginResult.Succeeded)
{
    AtProtoHttpResult<CreateRecordResult> postResult = await agent.Post("Hello World from idunno.Bluesky");

    if (!postResult.Succeeded)
    {
        Console.WriteLine($"Post failed with HTTP Status Code {postResult.StatusCode}");
    }
}
else
{
    Console.WriteLine($"Login failed with HTTP Status Code {loginResult.StatusCode}");
    if (loginResult.AtErrorDetail is not null)
    {
        Console.Write($"API Error {loginResult.AtErrorDetail.Error} {loginResult.AtErrorDetail.Message}")
    }
}
