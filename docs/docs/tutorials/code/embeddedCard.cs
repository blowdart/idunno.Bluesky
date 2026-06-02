Uri uri = new("https://en.wikipedia.org/wiki/Heinz_Baked_Beans");
var cardGenerator = agent.CreateOpenGraphEmbeddedCardGenerator();

var post = new Post($"An embedded card");
var card = await cardGenerator.Generate(uri, cancellationToken);
if (card != null)
{
    post.Embed(card);
}
await agent.Post(post, cancellationToken: cancellationToken);

Uri uri = new("https://lab.leaflet.pub/3mmwnyfqhyc2d");
var cardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

var post = new Post($"Leaflet Lab Notes - The Standardverse expands skyward!");
var card = await cardGenerator.Generate(uri, cancellationToken);
if (card != null)
{
    post.Embed(card);
}
await agent.Post(post, cancellationToken: cancellationToken);

