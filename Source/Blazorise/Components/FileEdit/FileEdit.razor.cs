#region Using directives
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise.Extensions;
using Blazorise.Localization;
using Blazorise.Modules;
using Blazorise.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
#endregion

namespace Blazorise
{
    /// <summary>
    /// Input component with support for single of multi file upload.
    /// </summary>
    public partial class FileEdit : BaseInputComponent<IFileEntry[]>, IFileEdit,
        IFileEntryOwner,
        IFileEntryNotifier,
        IAsyncDisposable
    {
        #region Members

        private bool multiple;

        // taken from https://github.com/aspnet/AspNetCore/issues/11159
        private DotNetObjectReference<FileEditAdapter> dotNetObjectRef;

        private IFileEntry[] files;

        #endregion

        #region Methods

        /// <summary>
        /// Gets the internal progress state for the current item being processed.
        /// </summary>
        /// <returns></returns>
        internal (double Progress, long ProgressProgress, long ProgressTotal) GetCurrentProgress()
            => (Progress, ProgressProgress, ProgressTotal);

        /// <inheritdoc/>
        public override async Task SetParametersAsync( ParameterView parameters )
        {
            await base.SetParametersAsync( parameters );

            if ( ParentValidation != null )
            {
                await InitializeValidation();
            }
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            LocalizerService.LocalizationChanged += OnLocalizationChanged;

            base.OnInitialized();
        }

        /// <summary>
        /// Handles the localization changed event.
        /// </summary>
        /// <param name="sender">Object that raised the event.</param>
        /// <param name="eventArgs">Data about the localization event.</param>
        private async void OnLocalizationChanged( object sender, EventArgs eventArgs )
        {
            // no need to refresh if we're using custom localization
            if ( BrowseButtonLocalizer != null )
                return;

            await InvokeAsync( StateHasChanged );
        }

        /// <inheritdoc/>
        protected override void BuildClasses( ClassBuilder builder )
        {
            builder.Append( ClassProvider.FileEdit() );
            builder.Append( ClassProvider.FileEditValidation( ParentValidation?.Status ?? ValidationStatus.None ), ParentValidation?.Status != ValidationStatus.None );

            base.BuildClasses( builder );
        }

        /// <inheritdoc/>
        protected override async Task OnFirstAfterRenderAsync()
        {
            dotNetObjectRef ??= CreateDotNetObjectRef( new FileEditAdapter( this ) );

            await JSFileEditModule.Initialize( dotNetObjectRef, ElementRef, ElementId );

            await base.OnFirstAfterRenderAsync();
        }

        /// <inheritdoc/>
        protected override async ValueTask DisposeAsync( bool disposing )
        {
            if ( disposing && Rendered )
            {
                await JSFileEditModule.SafeDestroy( ElementRef, ElementId );

                DisposeDotNetObjectRef( dotNetObjectRef );
                dotNetObjectRef = null;

                LocalizerService.LocalizationChanged -= OnLocalizationChanged;
            }

            await base.DisposeAsync( disposing );
        }

        /// <summary>
        /// Notifies the component that file input value has changed.
        /// </summary>
        /// <param name="files">List of changed file(s).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task NotifyChange( FileEntry[] files )
        {
            // Unlike in other Edit components we cannot just call CurrentValueHandler since
            // we're dealing with complex types instead of a simple string as en element value.
            //
            // Because of that we're going to skip CurrentValueHandler and implement all the
            // update logic here.

            var updatedFiles = InternalValue?.Where(x=> files.Any(file => file.Id == x.Id)).ToList() ?? new();
            foreach ( var file in files )
            {
                if ( !updatedFiles.Any( x => x.Id == file.Id ) )
                {
                    // So that method invocations on the file can be dispatched back here
                    file.Owner = (IFileEntryOwner)(object)this;

                    if ( MaxFileSize < file.Size )
                        file.Status = FileEntryStatus.ExceedsMaximumSize;

                    updatedFiles.Add( file );
                }
            }

            InternalValue = updatedFiles.ToArray();

            // send the value to the validation for processing
            if ( ParentValidation != null )
                await ParentValidation.NotifyInputChanged<IFileEntry[]>( files );

            await Changed.InvokeAsync( new( files ) );

            await InvokeAsync( StateHasChanged );
        }

        /// <inheritdoc/>
        protected override Task OnInternalValueChanged( IFileEntry[] value )
        {
            throw new NotImplementedException( $"{nameof( OnInternalValueChanged )} in {nameof( FileEdit )} should never be called." );
        }

        /// <inheritdoc/>
        protected override Task<ParseValue<IFileEntry[]>> ParseValueFromStringAsync( string value )
        {
            throw new NotImplementedException( $"{nameof( ParseValueFromStringAsync )} in {nameof( FileEdit )} should never be called." );
        }

        /// <inheritdoc/>
        public Task UpdateFileStartedAsync( IFileEntry fileEntry )
        {
            ProgressProgress = 0;
            ProgressTotal = fileEntry.Size;
            Progress = 0;

            fileEntry.Status = FileEntryStatus.Uploading;
            return Started.InvokeAsync( new( fileEntry ) );
        }

        /// <inheritdoc/>
        public async Task UpdateFileEndedAsync( IFileEntry fileEntry, bool success, FileInvalidReason fileInvalidReason )
        {
            if ( success )
                fileEntry.Status = FileEntryStatus.Uploaded;
            else
                fileEntry.Status = FileEntryStatus.Error;

            if ( AutoReset )
            {
                await Reset();
            }

            await Ended.InvokeAsync( new( fileEntry, success, fileInvalidReason ) );
        }

        /// <inheritdoc/>
        public Task UpdateFileWrittenAsync( IFileEntry fileEntry, long position, byte[] data )
        {
            return Written.InvokeAsync( new( fileEntry, position, data ) );
        }

        /// <inheritdoc/>
        public Task UpdateFileProgressAsync( IFileEntry fileEntry, long progressProgress )
        {
            ProgressProgress += progressProgress;

            var progress = Math.Round( (double)ProgressProgress / ProgressTotal, 3 );

            if ( Math.Abs( progress - Progress ) > double.Epsilon )
            {
                Progress = progress;

                return Progressed.InvokeAsync( new( fileEntry, Progress ) );
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task WriteToStreamAsync( FileEntry fileEntry, Stream stream, CancellationToken cancellationToken = default )
        {
            return new RemoteFileEntryStreamReader( JSFileModule, ElementRef, fileEntry, this, MaxChunkSize, MaxFileSize )
                .WriteToStreamAsync( stream, cancellationToken );
        }

        /// <inheritdoc/>
        public Stream OpenReadStream( FileEntry fileEntry, CancellationToken cancellationToken = default )
        {
            return new RemoteFileEntryStream( JSFileModule, ElementRef, fileEntry, this, MaxChunkSize, SegmentFetchTimeout, MaxFileSize, cancellationToken );
        }

        /// <summary>
        /// Manually resets the input file value.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public ValueTask Reset()
        {
            ProgressProgress = 0;
            ProgressTotal = 0;
            Progress = 0;
            return JSFileEditModule.Reset( ElementRef, ElementId );
        }

        /// <summary>
        /// Removes a file from the current file selection.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public ValueTask RemoveFile( int fileId )
        {
            return JSFileEditModule.RemoveFile( ElementRef, ElementId, fileId );
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently added files.
        /// </summary>
        public IFileEntry[] Files => InternalValue;

        /// <inheritdoc/>
        protected override bool ShouldAutoGenerateId => true;

        /// <inheritdoc/>
        protected override IFileEntry[] InternalValue { get => files; set => files = value; }

        /// <summary>
        /// Number of processed bytes in current file.
        /// </summary>
        protected long ProgressProgress;

        /// <summary>
        /// Total number of bytes in currently processed file.
        /// </summary>
        protected long ProgressTotal;

        /// <summary>
        /// Percentage of the current file-read status.
        /// </summary>
        protected double Progress;

        /// <summary>
        /// Gets or sets the <see cref="IJSFileEditModule"/> instance.
        /// </summary>
        [Inject] public IJSFileEditModule JSFileEditModule { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IJSFileModule"/> instance.
        /// </summary>
        [Inject] public IJSFileModule JSFileModule { get; set; }

        /// <summary>
        /// Gets or sets the DI registered <see cref="ITextLocalizerService"/>.
        /// </summary>
        [Inject] protected ITextLocalizerService LocalizerService { get; set; }

        /// <summary>
        /// Gets or sets the DI registered <see cref="ITextLocalizer{FileEdit}"/>.
        /// </summary>
        [Inject] protected ITextLocalizer<FileEdit> Localizer { get; set; }

        /// <summary>
        /// Gets the localized browse button text.
        /// </summary>
        protected string BrowseButtonString
        {
            get
            {
                var localizationString = Multiple
                    ? "Choose files"
                    : "Choose file";

                if ( BrowseButtonLocalizer != null )
                    return BrowseButtonLocalizer.Invoke( localizationString );

                return Localizer[localizationString];
            }
        }

        /// <summary>
        /// Gets the list is selected filename
        /// </summary>
        protected IEnumerable<string> SelectedFileNames => InternalValue?.Select( x => x.Name ) ?? Enumerable.Empty<string>();

        /// <summary>
        /// Enables the multiple file selection.
        /// </summary>
        [Parameter]
        public bool Multiple
        {
            get => multiple;
            set
            {
                multiple = value;

                DirtyClasses();
            }
        }

        /// <summary>
        /// Sets the placeholder for the empty file input.
        /// </summary>
        [Parameter] public string Placeholder { get; set; }

        /// <summary>
        /// Specifies the types of files that the input accepts. https://www.w3schools.com/tags/att_input_accept.asp"
        /// </summary>
        [Parameter] public string Filter { get; set; }

        /// <summary>
        /// Gets or sets the max chunk size when uploading the file.
        /// </summary>
        [Parameter] public int MaxChunkSize { get; set; } = 20 * 1024;

        /// <summary>
        /// Maximum file size in bytes, checked before starting upload (note: never trust client, always check file
        /// size at server-side). Defaults to <see cref="long.MaxValue"/>.
        /// </summary>
        [Parameter] public long MaxFileSize { get; set; } = long.MaxValue;

        /// <summary>
        /// Gets or sets the Segment Fetch Timeout when uploading the file.
        /// </summary>
        [Parameter] public TimeSpan SegmentFetchTimeout { get; set; } = TimeSpan.FromMinutes( 1 );

        /// <summary>
        /// Occurs every time the selected file has changed, including when the reset operation is executed.
        /// </summary>
        [Parameter] public EventCallback<FileChangedEventArgs> Changed { get; set; }

        /// <summary>
        /// Occurs when an individual file upload has started.
        /// </summary>
        [Parameter] public EventCallback<FileStartedEventArgs> Started { get; set; }

        /// <summary>
        /// Occurs when an individual file upload has ended.
        /// </summary>
        [Parameter] public EventCallback<FileEndedEventArgs> Ended { get; set; }

        /// <summary>
        /// Occurs every time the part of file has being written to the destination stream.
        /// </summary>
        [Parameter] public EventCallback<FileWrittenEventArgs> Written { get; set; }

        /// <summary>
        /// Notifies the progress of file being written to the destination stream.
        /// </summary>
        [Parameter] public EventCallback<FileProgressedEventArgs> Progressed { get; set; }

        /// <summary>
        /// If true file input will be automatically reset after it has being uploaded.
        /// </summary>
        [Parameter] public bool AutoReset { get; set; } = true;

        /// <summary>
        /// Function used to handle custom localization that will override a default <see cref="ITextLocalizer"/>.
        /// </summary>
        [Parameter] public TextLocalizerHandler BrowseButtonLocalizer { get; set; }

        #endregion
    }
}
