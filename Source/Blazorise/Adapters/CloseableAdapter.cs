﻿#region Using directives
using System.Threading.Tasks;
#endregion

namespace Blazorise;

/// <summary>
/// Manages the <see cref="IHideableComponent"/> visibility and animation logic.
/// </summary>
public class CloseableAdapter
{
    private readonly IHideableComponent component;

    /// <summary>
    /// Default constructor for <see cref="CloseableAdapter"/>.
    /// </summary>
    /// <param name="component">Reference to the close activator.</param>
    public CloseableAdapter( IHideableComponent component )
    {
        this.component = component;
    }

    /// <summary>
    /// Runs <see cref="IHideableComponent"/> procedure.
    /// </summary>
    /// <param name="visible">Visibility flag.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Run( bool visible )
    {
        if ( component is IAnimatedComponent animatedComponent && animatedComponent.Animated )
        {
            await animatedComponent.BeginAnimation( visible );

            if ( visible )
                // The 10ms delay is selected based on testing.
                // Task.Yield works for Blazor WebAssembly, but not for Blazor Server apps.
                // A small delay works well for both.
                await Task.Delay( 10 );
            else
                await Task.Delay( animatedComponent.AnimationDuration );

            await animatedComponent.EndAnimation( visible );
        }
        else
        {
            if ( visible )
                await component.Show();
            else
                await component.Hide();
        }
    }
}