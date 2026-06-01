# Embedded cards in posts

Bluesky supports embedding content from external websites in posts, through cards. A card can be created
from [Open Graph](https://ogp.me/ metadata, or [standard.site](https://standard.site) known metadata embedded in the page linked to.

To create a card you create an instance of a card generator, call the `Generate` method, and
then attach the results to a post.

For example, to create a card from Open Graph metadata:

[!code-csharp[](code/embeddedCard.cs#L1-L10)]

This will create a Bluesky post that has a card attached to it, rendered in a client that supports cards.

![An embedded card for Wikipedia's page on Baked Beans](../media/embeddedCard.png "An embedded Card")

Card generator requires the agent to be authenticated as, if there is image metadata, the agent will
download the image and upload it to the user's personal data store as a blob.

You may notice that the URI for the code does not need to appear in the text of the post, as the card is attached to the post separately.
This can be useful for creating more engaging posts that don't rely on links in the text.

site.standard metadata takes the card functionality a step further, the Bluesky client embeds interactive buttons
to subscribe to the publication, or read the document.

![An embedded card for a leaflet.pub document](../media/standardSiteEmbeddedCard.png "An embedded card for a standard.site compatible document")

[!code-csharp[](code/embeddedCard.cs#L12-L21)]

Posts can only have one card attached to them, so if you have multiple links in a post, you will need to choose which one to create a card for.

> [!TIP]
> The standard.site metadata format is a superset of Open Graph, so you can the StandardSiteCardGenerator to create cards from Open Graph metadata.
> Practically, this means you always use the StandardSiteCardGenerator without worrying about losing support for Open Graph metadata.
>
> If there is no metadata present in the URI you want to generate a card for card generators will return `null`.
