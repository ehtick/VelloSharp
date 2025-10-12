namespace HarfBuzzSharp;

public interface IFaceTableProvider
{
    Tag[] GetTableTags(Face face);

    Blob? GetTable(Face face, Tag tag);
}

