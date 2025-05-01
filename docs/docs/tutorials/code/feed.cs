int maximumPagesToRetrieve = 5;
int numberOfEntriesPerPage = 5;
int pageCount = 0;

var timelineResult =
    await agent.GetTimeline(limit: numberOfEntriesPerPage);

if (timelineResult.Succeeded && timelineResult.Result.Count != 0)
{
    do
    {
        // Do whatever needs to be done on the page of timeline entries.

        // Get the next page
        if (!string.IsNullOrEmpty(timelineResult.Result.Cursor))
        {
            timelineResult =
                await agent.GetTimeline(
                    limit: numberOfEntriesPerPage,
                    cursor: timelineResult.Result.Cursor, cancellationToken: cancellationToken);
        }

       pageCount++;

    } while (timelineResult.Succeeded &&
             !string.IsNullOrEmpty(timelineResult.Result.Cursor) &&
             pageCount < maximumPagesToRetrieve);
}
