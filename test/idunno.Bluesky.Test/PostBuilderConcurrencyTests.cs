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

    [Fact]
    public void ValidationErrorsDoesNotTearWhileTheBuilderIsBeingMutated()
    {
        // ValidationErrors used to read HasText, Text, HasImages, HasVideo and the rest one property at a time, taking
        // a separate snapshot under the lock for each. Text going away between the HasText check and the Text read
        // dereferenced null, and any pair of rules could be judged against a combination the builder never held. The
        // window is too narrow to reproduce on demand, so this is a guard against the property-at-a-time shape coming
        // back rather than a reproduction of the fault.
        Exception? mutatorException = null;
        Exception? validatorException = null;

        for (int iteration = 0; iteration < 200 && mutatorException is null && validatorException is null; iteration++)
        {
            PostBuilder builder = new("hello");
            builder.Add(CreateImage("alt"));

            using ManualResetEventSlim start = new(false);

            Thread mutator = new(() =>
            {
                start.Wait();

                try
                {
                    for (int i = 0; i < 500; i++)
                    {
                        builder.Text = null;
                        builder.Text = "hello";
                    }
                }
                catch (Exception ex)
                {
                    mutatorException = ex;
                }
            });

            Thread validator = new(() =>
            {
                start.Wait();

                try
                {
                    for (int i = 0; i < 500; i++)
                    {
                        // The builder always carries an image, so it is valid whether or not it currently has text.
                        Assert.Empty(builder.ValidationErrors());
                    }
                }
                catch (Exception ex)
                {
                    validatorException = ex;
                }
            });

            mutator.Start();
            validator.Start();
            start.Set();
            mutator.Join();
            validator.Join();
        }

        Assert.Null(mutatorException);
        Assert.Null(validatorException);
    }

    [Fact]
    public void ValidationErrorsAreProducedWhenItIsCalledRatherThanWhenItIsEnumerated()
    {
        PostBuilder builder = new();

        IEnumerable<string> errors = builder.ValidationErrors();

        builder.Text = "this makes the builder valid";

        Assert.NotEmpty(errors);
    }
}
