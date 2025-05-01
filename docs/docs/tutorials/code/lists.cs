using idunno.Bluesky;

using BlueskyAgent agent = new();
var loginResult = await agent.Login(handle, password);

if (loginResult.Succeeded)
{
    Did = agent.Did;

    // Get your own lists.
    var getListsResult = await agent.GetLists(
        actor: did);

    getListsResult.EnsureSucceeded();

    // Iterate through the lists
    foreach (ListView listView in listsForUserResult.Result)
    {
        Console.WriteLine(listView.Uri);
        Console.WriteLine($"\t{listView.Name} {listView.ListItemCount} entries");
        if (!string.IsNullOrEmpty(listView.Description))
        {
            Console.WriteLine($"\t{listView.Description}");
        }

        if (listView.ListItemCount != 0)
        {
            AtUri uri = listView.Uri;

            var getListResult = await agent.GetList(
                uri: uri);

            getListResult.EnsureSucceeded();

            Console.WriteLine(listView.Uri);
            Console.WriteLine($"\t{listView.Name} {listView.ListItemCount} entries");
            if (!string.IsNullOrEmpty(listView.Description))
            {
                Console.WriteLine($"\t{listView.Description}");
            }

            do
            {
                foreach (ListItemView listEntry in getListResult.Result)
                {
                    Console.WriteLine($"\t\t{listEntry.Uri}");
                    Console.WriteLine($"\t\t\t{listEntry.Subject.Did}");
                    Console.WriteLine($"\t\t\t{listEntry.Subject.Handle}");
                }

                if (!string.IsNullOrEmpty(getListResult.Result.Cursor))
                {
                    getListResult = await agent.GetList(
                        uri: listView.Uri,
                        cursor: getListResult.Result.Cursor);
                }

            } while (getListResult.Succeeded && !string.IsNullOrEmpty(getListResult.Result.Cursor));
        }
    }
