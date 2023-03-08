namespace DeveloperMenu
{
    public class CommandData
    {
        public string command;
        public string label;
        public bool close;

        public CommandData(string v1, string v2, bool v3)
        {
            command = v1;
            label = v2;
            close = v3;
        }
    }
}