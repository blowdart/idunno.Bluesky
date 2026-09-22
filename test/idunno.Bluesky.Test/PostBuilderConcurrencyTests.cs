// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class PostBuilderConcurrencyTests
{
    private static EmbeddedImage CreateImage(string altText) =>
        new(new Blob(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), altText);

    [Fact]
    public void EqualsDoesNotThrowWhileTheOtherBuilderIsBeingMutated()
    {
        // Equals used to read the other builder's collections without holding its lock, which threw
        // "Collection was modified" if that builder was being added to on another thread.
        Exception? mutatorException = null;
        Exception? comparerException = null;

        for (int iteration = 0; iteration < 200; iteration++)
        {
            PostBuilder left = new("hello");
            PostBuilder right = new("hello");

            using ManualResetEventSlim start = new(false);

            Thread mutator = new(() =>
            {
                start.Wait();

                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        right.Add(CreateImage($"alt {i}"));
                    }
                }
                catch (Exception ex)
                {
                    mutatorException = ex;
                }
            });

            Thread comparer = new(() =>
            {
                start.Wait();

                try
                {
                    for (int i = 0; i < 200; i++)
                    {
                        _ = left.Equals(right);
                    }
                }
                catch (Exception ex)
                {
                    comparerException = ex;
                }
            });

            mutator.Start();
            comparer.Start();
            start.Set();
            mutator.Join();
            comparer.Join();

            Assert.Null(mutatorException);
            Assert.Null(comparerException);
        }
    }
}
