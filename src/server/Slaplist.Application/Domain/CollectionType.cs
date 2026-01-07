namespace Slaplist.Application.Domain;

public enum CollectionType
{
    // YouTube
    Playlist = 1,
    
    // Discogs
    Collection = 2,     // User's owned records
    Wantlist = 3,       // User's wanted records
    ForSale = 4,        // Seller's inventory
    
    // Bandcamp
    Purchases = 5,      // User's bought tracks
    Wishlist = 6        // User's saved-for-later
}