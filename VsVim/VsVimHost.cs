﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Media;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    internal sealed class VsVimHost : IVimHost
    {
        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly ITextBufferUndoManagerProvider _undoManagerProvider;
        private readonly _DTE _dte;
        private readonly IVsTextManager _textManager;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        [ImportingConstructor]
        internal VsVimHost(
            ITextBufferUndoManagerProvider undoManagerProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            SVsServiceProvider serviceProvider)
        {
            _undoManagerProvider = undoManagerProvider;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _textManager = (IVsTextManager)serviceProvider.GetService(typeof(SVsTextManager));
        }

        private void UpdateStatus(string text)
        {
            _dte.StatusBar.Text = text;
        }

        private bool SafeExecuteCommand(string command, string args = "" )
        {
            try
            {
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The C++ project system requires that the target of GoToDefinition be passed
        /// as an argument to the command.  
        /// </summary>
        private bool GoToDefinitionCPlusPlus(IWpfTextView textView)
        {
            var caretPoint = textView.Caret.Position.BufferPosition;
            var span = TssUtil.FindCurrentFullWordSpan(caretPoint, WordKind.NormalWord);
            if (span.IsSome())
            {
                return SafeExecuteCommand(CommandNameGoToDefinition, span.Value.GetText());
            }
            else
            {
                return SafeExecuteCommand(CommandNameGoToDefinition);
            }
        }

        #region IVimHost

        void IVimHost.Beep()
        {
            SystemSounds.Beep.Play();
        }

        void IVimHost.OpenFile(string file)
        {
            var names = _dte.GetProjects().SelectMany(x => x.GetProjecItems()).Select(x => x.Name).ToList();
            var list = _dte.GetProjectItems(file);

            if (list.Any())
            {
                var item = list.First();
                var result = item.Open(EnvDTE.Constants.vsViewKindPrimary);
                result.Activate();
                return;
            }

            Console.Beep();
        }

        bool IVimHost.GoToDefinition()
        {
            var tuple = _textManager.TryGetActiveTextView(_editorAdaptersFactoryService);
            if (tuple.Item1 && tuple.Item2.TextBuffer.ContentType.IsCPlusPlus())
            {
                return GoToDefinitionCPlusPlus(tuple.Item2);
            }

            return SafeExecuteCommand(CommandNameGoToDefinition);
        }

        bool IVimHost.GoToMatch()
        {
            return SafeExecuteCommand("Edit.GoToBrace");
        }

        void IVimHost.ShowOpenFileDialog()
        {
            SafeExecuteCommand("Edit.OpenFile");
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            var snapshotLine = point.Position.GetContainingLine();
            var column = point.Position.Position - snapshotLine.Start.Position;
            var vsBuffer = _editorAdaptersFactoryService.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
            var viewGuid = VSConstants.LOGVIEWID_Code;
            var hr = _textManager.NavigateToLineAndColumn(
                vsBuffer,
                ref viewGuid,
                snapshotLine.LineNumber,
                column,
                snapshotLine.LineNumber,
                column);
            return ErrorHandler.Succeeded(hr);
        }

        string IVimHost.GetName(ITextBuffer buffer)
        {
            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(buffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return String.Empty;
            }
            return vsTextLines.GetFileName();
        }

        void IVimHost.SaveCurrentFile()
        {
            SafeExecuteCommand("File.SaveSelectedItems");
        }

        void IVimHost.SaveCurrentFileAs(string fileName)
        {
            SafeExecuteCommand("File.SaveSelectedItemsAs " + fileName);
        }

        void IVimHost.SaveAllFiles()
        {
            SafeExecuteCommand("File.SaveAll");
        }

        void IVimHost.CloseCurrentFile(bool checkDirty)
        {
            SafeExecuteCommand("File.Close");
        }

        void IVimHost.CloseAllFiles(bool checkDirty)
        {
            SafeExecuteCommand("Window.CloseAllDocuments");
        }

        void IVimHost.GoToNextTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.NextDocumentWindow");
                count--;
            }
        }

        void IVimHost.GoToPreviousTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.PreviousDocumentWindow");
                count--;
            }
        }

        void IVimHost.BuildSolution()
        {
            SafeExecuteCommand("Build.BuildSolution");
        }

        #endregion
    }
}
