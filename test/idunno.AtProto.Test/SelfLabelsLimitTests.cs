// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Labels;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class SelfLabelsLimitTests
{
    private static List<SelfLabel> Labels(int count)
    {
        List<SelfLabel> labels = [];

        for (int i = 0; i < count; i++)
        {
            labels.Add(new SelfLabel($"label{i}"));
        }

        return labels;
    }

    [Fact]
    public void MaximumLabelsIsTen()
    {
        Assert.Equal(10, SelfLabels.MaximumLabels);
    }

    [Fact]
    public void ConstructorAcceptsTheMaximumNumberOfLabels()
    {
        SelfLabels selfLabels = new(Labels(SelfLabels.MaximumLabels));

        Assert.Equal(SelfLabels.MaximumLabels, selfLabels.Values.Count);
    }

    [Fact]
    public void ConstructorThrowsWhenGivenMoreThanTheMaximumNumberOfLabels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelfLabels(Labels(SelfLabels.MaximumLabels + 1)));
    }

    [Fact]
    public void ValuesSetterThrowsWhenGivenMoreThanTheMaximumNumberOfLabels()
    {
        SelfLabels selfLabels = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => selfLabels.Values = Labels(SelfLabels.MaximumLabels + 1));
    }

    [Fact]
    public void AddLabelThrowsOnceTheMaximumNumberOfLabelsHasBeenReached()
    {
        SelfLabels selfLabels = new(Labels(SelfLabels.MaximumLabels));

        Assert.Throws<ArgumentOutOfRangeException>(() => selfLabels.AddLabel("oneTooMany"));
        Assert.Equal(SelfLabels.MaximumLabels, selfLabels.Values.Count);
    }

    [Fact]
    public void AddLabelDoesNotThrowWhenTheLabelIsAlreadyPresentAtTheMaximum()
    {
        List<SelfLabel> labels = Labels(SelfLabels.MaximumLabels);
        SelfLabels selfLabels = new(labels);

        selfLabels.AddLabel(labels[0].Value);

        Assert.Equal(SelfLabels.MaximumLabels, selfLabels.Values.Count);
    }

    [Fact]
    public void AddLabelFillsUpToTheMaximumOneAtATime()
    {
        SelfLabels selfLabels = new();

        for (int i = 0; i < SelfLabels.MaximumLabels; i++)
        {
            selfLabels.AddLabel($"label{i}");
        }

        Assert.Equal(SelfLabels.MaximumLabels, selfLabels.Values.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => selfLabels.AddLabel("oneTooMany"));
    }
}
