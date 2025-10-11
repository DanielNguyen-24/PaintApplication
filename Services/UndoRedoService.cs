using System.Collections.Generic;

namespace PaintApplication.Services
{
    public class UndoRedoService
    {
        private readonly Stack<object> _undo = new();
        private readonly Stack<object> _redo = new();

        public void PushUndo(object state) { _undo.Push(state); _redo.Clear(); }
        public object? Undo() { if (_undo.Count == 0) return null; var s = _undo.Pop(); _redo.Push(s); return s; }
        public object? Redo() { if (_redo.Count == 0) return null; var s = _redo.Pop(); _undo.Push(s); return s; }
        public void Clear() { _undo.Clear(); _redo.Clear(); }
    }
}
