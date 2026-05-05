namespace SongsOfConquestAccess.Speech.Spatial
{
    internal interface ISpatialTileSpeechFormatter<TTile>
    {
        string DescribeTile(TTile tile);

        string DescribePrimaryContent(TTile tile);

        string DescribeTileContext(TTile tile);

        string DescribeCoordinates(TTile tile);
    }
}
