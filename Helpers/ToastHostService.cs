using BlazorBootstrap;

namespace UtiliExtract.Helpers
{
    public class ToastHostService
    {
        public event Action<ToastMessage> OnShow = default!;

        private void Notify(ToastMessage msg) => OnShow?.Invoke(msg);

        public void ShowInfo(string message) =>
            Notify(new ToastMessage { Type = ToastType.Info, Message = message });

        public void ShowSuccess(string message) =>
            Notify(new ToastMessage { Type = ToastType.Success, Message = message });

        public void ShowWarning(string message) =>
            Notify(new ToastMessage { Type = ToastType.Warning, Message = message });

        public void ShowError(string message) =>
            Notify(new ToastMessage { Type = ToastType.Danger, Message = $"An Error Occurred: {message}" });
    }
}