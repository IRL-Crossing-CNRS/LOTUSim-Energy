namespace Lotusim
{
    public interface IHudPanel
    {
        bool IsOpen { get; }
        void SetOpen(bool open);
    }
}
