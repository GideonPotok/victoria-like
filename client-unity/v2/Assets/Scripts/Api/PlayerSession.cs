namespace VictoriaLike.Client.Api
{
    public static class PlayerSession
    {
        public static string Token { get; private set; }
        public static string ActorId { get; private set; }
        public static string Username { get; private set; }
        public static string ControlledCountryId { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public static void Set(string token, string actorId, string username, string controlledCountryId)
        {
            Token = token;
            ActorId = actorId;
            Username = username;
            ControlledCountryId = controlledCountryId;
        }

        public static void Clear()
        {
            Token = ActorId = Username = ControlledCountryId = null;
        }
    }
}
