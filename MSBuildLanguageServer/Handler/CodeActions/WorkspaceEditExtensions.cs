// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

using Roslyn.LanguageServer.Protocol;
using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

static class WorkspaceEditExtensions
{
    public static WorkspaceEdit? ToLspWorkspaceEdit(this MSBuildWorkspaceEdit edit, bool includeAnnotations, ClientCapabilities clientCapabilities)
    {
        bool supportsDocumentChanges = false;
        bool supportsCreateFile = false;
        bool supportsRenameFile = false;
        bool supportsDeleteFile = false;

        if(clientCapabilities.Workspace?.WorkspaceEdit is { } workspaceEditSetting)
        {
            supportsDocumentChanges = workspaceEditSetting.DocumentChanges;

            if(workspaceEditSetting.ResourceOperations is { } resourceOperationsSetting)
            {
                foreach(var supportedOperation in workspaceEditSetting.ResourceOperations)
                {
                    switch(supportedOperation.Value)
                    {
                    case "delete":
                        supportsDeleteFile = true;
                        break;
                    case "create":
                        supportsCreateFile = true;
                        break;
                    case "rename":
                        supportsRenameFile = true;
                        break;
                    default:
                        // TODO: log warning
                        break;
                    }
                }
            }
        }

        Func<MSBuildChangeAnnotation?, ChangeAnnotationIdentifier?>? convertAnnotation;

        Dictionary<ChangeAnnotationIdentifier, ChangeAnnotation>? annotations = null;
        Dictionary<MSBuildChangeAnnotation, ChangeAnnotationIdentifier>? annotationIds = null;

        if(includeAnnotations)
        {
            annotations = [];
            annotationIds = [];
            int nextId = 0;
            convertAnnotation = (MSBuildChangeAnnotation? annotation) => {
                if(annotation is null)
                {
                    return null;
                }
                if(!annotationIds.TryGetValue(annotation, out ChangeAnnotationIdentifier annotationId))
                {
                    annotationIds[annotation] = annotationId = new($"annotation_{nextId++}");
                    annotations[annotationId] = new ChangeAnnotation {
                        Label = annotation.Label,
                        Description = annotation.Description,
                        NeedsConfirmation = annotation.NeedsConfirmation,
                    };
                }
                return annotationId;
            };
        } else
        {
            convertAnnotation = (MSBuildChangeAnnotation? annotation) => null;
        }

        var documentChanges = new List<SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>>();

        foreach(var operation in edit.Operations)
        {
            switch(operation)
            {
            case MSBuildDocumentEdit docEdit:
                documentChanges.Add(docEdit.ToLspTextDocumentEdit(convertAnnotation));
                continue;

            case MSBuildDocumentCreate docCreate:
                if(supportsCreateFile)
                {
                    var converted = docCreate.ToLspCreateFile(convertAnnotation);
                    documentChanges.Add(converted.create);
                    if (converted.addContent != null)
                    {
                        documentChanges.Add(converted.addContent);
                    }
                    continue;
                }
                throw new UnsupportedCodeActionOperationException(operation, false);

            case MSBuildDocumentRename docRename:
                if(supportsRenameFile)
                {
                    documentChanges.Add(docRename.ToLspRenameFile(convertAnnotation));
                    continue;
                }
                throw new UnsupportedCodeActionOperationException(operation, false);

            case MSBuildDocumentDelete docDelete:
                if(supportsDeleteFile)
                {
                    documentChanges.Add(docDelete.ToLspDeleteFile(convertAnnotation));
                    continue;
                }
                throw new UnsupportedCodeActionOperationException(operation, false);
            }
            
            throw new UnsupportedCodeActionOperationException(operation, true);
        }

        // TODO: convert edits
        if (!supportsDocumentChanges)
        {
            throw new NotImplementedException();
        }

        var lspEdit = new WorkspaceEdit {
            ChangeAnnotations = annotations,
            DocumentChanges = documentChanges.ToArray()
        };

        return lspEdit;
    }

    static TextDocumentEdit ToLspTextDocumentEdit(this MSBuildDocumentEdit docEdit, Func<MSBuildChangeAnnotation?, ChangeAnnotationIdentifier?> convertAnnotation)
    {
        // TODO: handle MSBuildDocumentEdit selections. they don't map to an LSP feature so we will need to dispatch them to a command to be executed after applying the edit

        SourceText? sourceText = docEdit.OriginalText;

        //  TODO: load source text from workspace
        if(sourceText is null)
        {
            using var reader = File.OpenRead(docEdit.Filename);
            sourceText = SourceText.From(docEdit.Filename);
        }

        var convertedEdits = new List<SumType<TextEdit, AnnotatedTextEdit>>();

        foreach(var textEdit in docEdit.TextEdits)
        {
            var annotation = convertAnnotation(textEdit.Annotation) ?? null;

            var range = textEdit.Range.ToLspRange(sourceText);

            var convertedTextEdit = CreateTextEdit(textEdit.NewText, range, annotation);
            convertedEdits.Add(convertedTextEdit);
        }

        return new TextDocumentEdit {
            TextDocument = new OptionalVersionedTextDocumentIdentifier {
                Uri = ProtocolConversions.CreateAbsoluteUri(docEdit.Filename),
            },
            Edits = convertedEdits.ToArray()
        };
    }

    static SumType<TextEdit, AnnotatedTextEdit> CreateTextEdit(string content, LSP.Range range, ChangeAnnotationIdentifier? annotation)
    {
        if(annotation is null)
        {
            return new TextEdit {
                NewText = content,
                Range = range,
            };
        }

        return new AnnotatedTextEdit {
            AnnotationId = annotation.Value,
            NewText = content,
            Range = LocationHelpers.EmptyRange,

        };
    }

    static (CreateFile create, TextDocumentEdit? addContent) ToLspCreateFile(this MSBuildDocumentCreate docCreate, Func<MSBuildChangeAnnotation?, ChangeAnnotationIdentifier?> convertAnnotation)
    {
        CreateFileOptions? options = null;
        if (docCreate.IgnoreIfExists || docCreate.Overwrite)
        {
            options = new CreateFileOptions {
                IgnoreIfExists = docCreate.IgnoreIfExists,
                Overwrite = docCreate.Overwrite
            };
        }

        var annotation = convertAnnotation(docCreate.Annotation) ?? null;

        var creation = new CreateFile {
            Uri = ProtocolConversions.CreateAbsoluteUri(docCreate.Filename),
            Options = options,
            AnnotationId = annotation,
        };

        TextDocumentEdit? addContent = null;
        if (docCreate.Content is string content)
        {
            var edit = CreateTextEdit(content, LocationHelpers.EmptyRange, annotation);

            addContent = new TextDocumentEdit {
                TextDocument = new OptionalVersionedTextDocumentIdentifier {
                    Uri = creation.Uri
                },
                Edits = [edit]
            };
        }

        return (creation, addContent);
    }

    static DeleteFile ToLspDeleteFile(this MSBuildDocumentDelete docDelete, Func<MSBuildChangeAnnotation?, ChangeAnnotationIdentifier?> convertAnnotation)
    {
        DeleteFileOptions? options = null;
        if(docDelete.IgnoreIfNotExists || docDelete.Recursive)
        {
            options = new DeleteFileOptions {
                Recursive = docDelete.Recursive,
                IgnoreIfNotExists = docDelete.IgnoreIfNotExists
            };
        }

        var annotation = convertAnnotation(docDelete.Annotation) ?? null;

        return new DeleteFile {
            Uri = ProtocolConversions.CreateAbsoluteUri(docDelete.FileOrFolder),
            Options = options,
            AnnotationId = annotation,
        };
    }

    static RenameFile ToLspRenameFile(this MSBuildDocumentRename docRename, Func<MSBuildChangeAnnotation?, ChangeAnnotationIdentifier?> convertAnnotation)
    {
        RenameFileOptions? options = null;
        if(docRename.IgnoreIfExists || docRename.Overwrite)
        {
            options = new RenameFileOptions {
                IgnoreIfExists = docRename.IgnoreIfExists,
                Overwrite = docRename.Overwrite
            };
        }

        var annotation = convertAnnotation(docRename.Annotation) ?? null;

        return new RenameFile {
            OldUri = ProtocolConversions.CreateAbsoluteUri(docRename.OldFilename),
            NewUri = ProtocolConversions.CreateAbsoluteUri(docRename.NewFilename),
            Options = options,
            AnnotationId = annotation,
        };
    }
}
