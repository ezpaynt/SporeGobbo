using System;

[Serializable]
public sealed class CampaignFacts
{
    public int mushroomsEaten;
    public int weevilsKilled;
    public int blobsKilled;

    public void Normalize()
    {
        mushroomsEaten = Math.Max(0, mushroomsEaten);
        weevilsKilled = Math.Max(0, weevilsKilled);
        blobsKilled = Math.Max(0, blobsKilled);
    }

    public CampaignFacts Clone()
    {
        Normalize();
        return new CampaignFacts
        {
            mushroomsEaten = mushroomsEaten,
            weevilsKilled = weevilsKilled,
            blobsKilled = blobsKilled
        };
    }
}
