
namespace GeniePlugin.Plugins
{
    public interface IHost
    {

        void EchoText(string Text);

        void SendText(string Text);

        string get_Variable(string Var);
        void set_Variable(string Var, string value);

        System.Windows.Forms.Form ParentForm { get; }

        bool get_IsVerified(string key);

        bool get_IsPremium(string key);

        int InterfaceVersion { get; }

        string PluginKey { get; set; }

    }
}