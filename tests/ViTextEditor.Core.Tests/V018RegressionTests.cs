using ViTextEditor.Core.Editor;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class V018RegressionTests
{
    [Theory]
    [InlineData(':')]
    [InlineData('/')]
    [InlineData('?')]
    public void CommandAndSearchPrefixesAreNeverSupplementalMotions(char value)
    {
        Assert.True(ViSupplementalMotionRouting.IsCommandOrSearchPrefix(value));
        Assert.False(ViSupplementalMotionRouting.TryGetPunctuationMotion(value, out _));
    }

    [Theory]
    [InlineData('%')]
    [InlineData(';')]
    [InlineData(',')]
    [InlineData('(')]
    [InlineData(')')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('+')]
    [InlineData('-')]
    [InlineData('_')]
    [InlineData('|')]
    public void SupplementalPunctuationMotionsAreMappedByActualCharacter(char value)
    {
        Assert.False(ViSupplementalMotionRouting.IsCommandOrSearchPrefix(value));
        Assert.True(ViSupplementalMotionRouting.TryGetPunctuationMotion(value, out var token));
        Assert.Equal(value.ToString(), token);
    }
}
