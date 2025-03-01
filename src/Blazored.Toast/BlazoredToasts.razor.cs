﻿using Blazored.Toast.Configuration;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Blazored.Toast;

public partial class BlazoredToasts
{
    [Inject] private IToastService ToastService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public IconType IconType { get; set; } = IconType.Blazored;
    [Parameter] public string? InfoClass { get; set; }
    [Parameter] public string? InfoIcon { get; set; }
    [Parameter] public string? SuccessClass { get; set; }
    [Parameter] public string? SuccessIcon { get; set; }
    [Parameter] public string? WarningClass { get; set; }
    [Parameter] public string? WarningIcon { get; set; }
    [Parameter] public string? ErrorClass { get; set; }
    [Parameter] public string? ErrorIcon { get; set; }
    [Parameter] public ToastPosition Position { get; set; } = ToastPosition.TopRight;
    [Parameter] public int Timeout { get; set; } = 5;
    [Parameter] public int MaxToastCount { get; set; } = int.MaxValue;
    [Parameter] public bool RemoveToastsOnNavigation { get; set; }
    [Parameter] public bool ShowProgressBar { get; set; }
    [Parameter] public RenderFragment? CloseButtonContent { get; set; }
    [Parameter] public bool ShowCloseButton { get; set; } = true;
    [Parameter] public bool DisableTimeout { get; set; }
    [Parameter] public bool PauseProgressOnHover { get; set; } = false;
    [Parameter] public int ExtendedTimeout { get; set; }

    private List<ToastInstance> ToastList { get; set; } = new();
    private Queue<ToastInstance> ToastWaitingQueue { get; set; } = new();

    protected override void OnInitialized()
    {
        ToastService.OnShow += ShowToast;
        ToastService.OnShowComponent += ShowCustomToast;
        ToastService.OnClearAll += ClearAll;
        ToastService.OnClearToasts += ClearToasts;
        ToastService.OnClearCustomToasts += ClearCustomToasts;
        ToastService.OnClearQueue += ClearQueue;
        ToastService.OnClearQueueToasts += ClearQueueToasts;

        if (RemoveToastsOnNavigation)
        {
            NavigationManager.LocationChanged += ClearToasts;
        }

        if (IconType == IconType.Custom
            && string.IsNullOrWhiteSpace(InfoIcon)
            && string.IsNullOrWhiteSpace(SuccessIcon)
            && string.IsNullOrWhiteSpace(WarningIcon)
            && string.IsNullOrWhiteSpace(ErrorIcon))
        {
            throw new ArgumentException("IconType is Custom but icon parameters are not set.");
        }
    }

    private ToastSettings BuildCustomToastSettings(Action<ToastSettings>? settings)
    {
        var instanceToastSettings = new ToastSettings();
        settings?.Invoke(instanceToastSettings);
        instanceToastSettings.Timeout = instanceToastSettings.Timeout == 0 ? Timeout : instanceToastSettings.Timeout;
        instanceToastSettings.DisableTimeout ??= DisableTimeout;
        instanceToastSettings.PauseProgressOnHover ??= PauseProgressOnHover;
        instanceToastSettings.ExtendedTimeout ??= ExtendedTimeout;
        instanceToastSettings.Position ??= Position;
        instanceToastSettings.ShowProgressBar ??= ShowProgressBar;

        return instanceToastSettings;
    }

    private ToastSettings BuildToastSettings(ToastLevel level, Action<ToastSettings>? settings)
    {
        var toastInstanceSettings = new ToastSettings();
        settings?.Invoke(toastInstanceSettings);

        return level switch
        {
            ToastLevel.Error => BuildToastSettings(toastInstanceSettings, "blazored-toast-error", ErrorIcon, ErrorClass),
            ToastLevel.Info => BuildToastSettings(toastInstanceSettings, "blazored-toast-info", InfoIcon, InfoClass),
            ToastLevel.Success => BuildToastSettings(toastInstanceSettings, "blazored-toast-success", SuccessIcon, SuccessClass),
            ToastLevel.Warning => BuildToastSettings(toastInstanceSettings, "blazored-toast-warning", WarningIcon, WarningClass),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
    }

    private ToastSettings BuildToastSettings(ToastSettings toastInstanceSettings, string cssClassForLevel, string? configIcon, string? configAdditionalClasses)
    {
        string? additonalClasses = string.IsNullOrEmpty(toastInstanceSettings.AdditionalClasses) ? configAdditionalClasses : toastInstanceSettings.AdditionalClasses;

        return new ToastSettings(
            $"{cssClassForLevel} {additonalClasses}",
            toastInstanceSettings.IconType ?? IconType,
            toastInstanceSettings.Icon ?? configIcon ?? "",
            toastInstanceSettings.ShowProgressBar ?? ShowProgressBar,
            toastInstanceSettings.ShowCloseButton ?? ShowCloseButton,
            toastInstanceSettings.OnClick,
            toastInstanceSettings.Timeout == 0 ? Timeout : toastInstanceSettings.Timeout,
            toastInstanceSettings.DisableTimeout ?? DisableTimeout,
            toastInstanceSettings.PauseProgressOnHover ?? PauseProgressOnHover,
            toastInstanceSettings.ExtendedTimeout ?? ExtendedTimeout,
            toastInstanceSettings.Position ?? Position);
    }

    private void ShowToast(ToastLevel level, RenderFragment message, Action<ToastSettings>? toastSettings)
    {
        InvokeAsync(() =>
        {
            var settings = BuildToastSettings(level, toastSettings);
            var toast = new ToastInstance(message, level, settings);

            if (ToastList.Count < MaxToastCount)
            {
                ToastList.Add(toast);

                StateHasChanged();
            }
            else
            {
                ToastWaitingQueue.Enqueue(toast);
            }
        });
    }

    private void ShowCustomToast(Type contentComponent, ToastParameters? parameters, Action<ToastSettings>? settings)
    {
        InvokeAsync(() =>
        {
            var childContent = new RenderFragment(builder =>
            {
                var i = 0;
                builder.OpenComponent(i++, contentComponent);
                if (parameters is not null)
                {
                    foreach (var parameter in parameters.Parameters)
                    {
                        builder.AddAttribute(i++, parameter.Key, parameter.Value);
                    }
                }

                builder.CloseComponent();
            });

            var toastSettings = BuildCustomToastSettings(settings);
            var toastInstance = new ToastInstance(childContent, toastSettings);
            
            if (ToastList.Count < MaxToastCount)
            {
                ToastList.Add(toastInstance);
                StateHasChanged();
            }
            else 
            {
                ToastWaitingQueue.Enqueue(toastInstance);
            }    
        });
    }

    private void ShowEnqueuedToast()
    {
        InvokeAsync(() =>
        {
            var toast = ToastWaitingQueue.Dequeue();

            ToastList.Add(toast);

            StateHasChanged();
        });
    }

    public void RemoveToast(Guid toastId)
    {
        InvokeAsync(() =>
        {
            var toastInstance = ToastList.SingleOrDefault(x => x.Id == toastId);

            if (toastInstance is not null)
            {
                ToastList.Remove(toastInstance);
                StateHasChanged();
            }

            if (ToastWaitingQueue.Any())
            {
                ShowEnqueuedToast();
            }
        });
    }

    private void ClearToasts(object? sender, LocationChangedEventArgs args)
    {
        InvokeAsync(() =>
        {
            ToastList.Clear();
            StateHasChanged();

            if (ToastWaitingQueue.Any())
            {
                ShowEnqueuedToast();
            }
        });
    }

    private void ClearAll()
    {
        InvokeAsync(() =>
        {
            ToastList.Clear();
            StateHasChanged();
        });
    }

    private void ClearToasts(ToastLevel toastLevel)
    {
        InvokeAsync(() =>
        {
            ToastList.RemoveAll(x => x.CustomComponent is null && x.Level == toastLevel);
            StateHasChanged();
        });
    }

    private void ClearCustomToasts()
    {
        InvokeAsync(() =>
        {
            ToastList.RemoveAll(x => x.CustomComponent is not null);
            StateHasChanged();
        });
    }

    private void ClearQueue()
    {
        InvokeAsync(() =>
        {
            ToastWaitingQueue.Clear();
            StateHasChanged();
        });
    }

    private void ClearQueueToasts(ToastLevel toastLevel)
    {
        InvokeAsync(() =>
        {
            ToastWaitingQueue = new(ToastWaitingQueue.Where(x => x.Level != toastLevel));
            StateHasChanged();
        });
    }
}
