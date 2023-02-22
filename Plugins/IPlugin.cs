
namespace GeniePlugin.Plugins
{
    public interface IPlugin
    {

        string Name { get; }

        string Version { get; }

        string Description { get; }

        string Author { get; }

        void Initialize(IHost Host);

        void Show();

        void VariableChanged(string Variable);

        string ParseText(string Text, string Window);

        string ParseInput(string Text);

        void ParseXML(string XML);

        bool Enabled { get; set; }

        void ParentClosing();

    }
}