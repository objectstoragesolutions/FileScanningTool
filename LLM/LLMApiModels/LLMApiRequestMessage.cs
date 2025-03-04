namespace LLMApiModels
{
    public enum RequestMessageRoles
    {
        System,
        User,
        Assistant
    }

    public class LLMApiRequestMessage
    {
        public LLMApiRequestMessage(RequestMessageRoles role)
        {
            Role = role;
        }

        public LLMApiRequestMessage(RequestMessageRoles role, string messageText)
        {
            Role = role;
            TextContent = messageText;
        }

        public RequestMessageRoles Role { get; private set; }

        public string RoleString => Role.ToString().ToLower();

        public string TextContent { get; set; }

        public LLMApiRequestMessageContent Content { get; set; }
    }
}
