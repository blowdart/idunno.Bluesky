using idunno.Bluesky;

using BlueskyAgent agent = new();
await agent.Login(handle, password);
await agent.Post("Hello World from idunno.Bluesky");
