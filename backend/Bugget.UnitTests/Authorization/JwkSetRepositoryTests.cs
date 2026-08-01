using Bugget.Api.Authorization;
namespace Bugget.UnitTests.Authorization;

public class JwkSetRepositoryTests
{

    private const string _validJsonFilePath = "data/jwks/valid_jwks_pairs.json";
    private const string _invalidJsonFilePath = "data/jwks/invalid_jwks_pairs.json";

    [Fact]
    public async Task ValidFile_ReturnsSuccess()
    {
        // Arrange
        var validKeyPairs = await FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(_validJsonFilePath);
        var repository = JwkSetRepository.FromRsaKeyPairs(validKeyPairs);

        // Act
        var testKeyResult = await repository.GetJWKAsync("test-key-1");
        var testKeySetResult = await repository.GetJWKSetAsync();

        // Assert
        Assert.NotNull(repository);
        Assert.NotNull(testKeyResult);
        Assert.Equal("test-key-1", testKeyResult.KeyId);
        Assert.NotNull(testKeySetResult);
        Assert.NotNull(testKeySetResult);
        Assert.NotEmpty(testKeySetResult.Keys);
        Assert.Equal(2, testKeySetResult.Keys.Count);
    }

    // Test for a loading two keys with the same KeyId
    [Fact]
    public async Task DuplicateKeyId_ReturnsSuccess()
    {
        // Arrange
        var validKeyPairs = await FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(_invalidJsonFilePath);

        // Assert
        Assert.Throws<ArgumentException>(() => JwkSetRepository.FromRsaKeyPairs(validKeyPairs));
    }
}
