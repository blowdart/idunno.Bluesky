using idunno.Bluesky;

using BlueskyAgent agent = new();
var loginResult = await agent.Login(handle, password);

if (loginResult.Succeeded)
{
    var createListResult = await agent.CreateList(
        new BlueskyList(
            name: "Super Bean Fans",
            purpose: ListPurpose.CurateList,
            description: "People who realise the glory of Heinz Baked Beans."));

    createListResult.EnsureSucceeded();

    var uri = createListResult.Result.Uri;

    var listRecordResult = await agent.GetListRecord(uri: uri);

    listRecordResult.Result.Value.Description = "People who realise the glory of Heinz Baked Beans!";

    var updateListResult = await agent.UpdateList(list: listRecordResult.Result);

    updateListResult.EnsureSucceeded();

    Did did = "";

    var addResult = await agent.AddToList(
        uri: uri,
        did: did);

    var addResult = await agent.AddToList(
        uri: uri,
        handle: "blowdart.me");

    addResult.EnsureSucceeded();

    var deleteListEntryResult = await agent.DeleteFromList(
        uri: uri,
        did: did);

    var deleteListEntryResult = await agent.DeleteFromList(
        uri: uri,
        handle: "blowdart.me");

    deleteListEntryResult.EnsureSucceeded();

    var deleteListResult = await agent.DeleteList(
        uri: uri);

    deleteListResult.EnsureSucceeded();
}
