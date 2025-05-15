using MongoDB.Bson;

namespace Server.Models;

public class User
{
    public ObjectId Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
}