namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// 一次可撤销的编辑器动作：以 do/undo 两个委托描述。
    /// <para>
    /// 覆盖节点/边/充电桩的 新增、删除、移动、改属性 等操作。执行时调用 <see cref="Redo"/>，
    /// 撤销时调用 <see cref="Undo"/>。轻量实现，不引入第三方库。
    /// </para>
    /// </summary>
    public sealed class EditorAction
    {
        /// <summary>动作的人类可读描述（用于调试/状态栏，可选）。</summary>
        public string Description { get; }

        /// <summary>重做（即首次执行）逻辑。</summary>
        public Action Redo { get; }

        /// <summary>撤销逻辑（应精确还原 <see cref="Redo"/> 的副作用）。</summary>
        public Action Undo { get; }

        /// <summary>构造一个可撤销动作。</summary>
        /// <param name="description">动作描述。</param>
        /// <param name="redo">重做/执行委托。</param>
        /// <param name="undo">撤销委托。</param>
        public EditorAction(string description, Action redo, Action undo)
        {
            Description = description;
            Redo = redo;
            Undo = undo;
        }
    }

    /// <summary>
    /// 撤销/重做栈：执行动作时压入撤销栈并清空重做栈；撤销时在两栈间搬移。
    /// </summary>
    public sealed class UndoRedoStack
    {
        private readonly Stack<EditorAction> _undo = new();
        private readonly Stack<EditorAction> _redo = new();

        /// <summary>是否有可撤销动作。</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>是否有可重做动作。</summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>执行一个动作（调用其 Redo），压入撤销栈并清空重做栈。</summary>
        /// <param name="action">要执行的动作。</param>
        public void Do(EditorAction action)
        {
            action.Redo();
            _undo.Push(action);
            _redo.Clear();
        }

        /// <summary>登记一个已经发生的动作（不重复执行），用于拖拽这类"先改后记"的场景。</summary>
        /// <param name="action">已发生的动作。</param>
        public void Push(EditorAction action)
        {
            _undo.Push(action);
            _redo.Clear();
        }

        /// <summary>撤销栈顶动作并转入重做栈。栈空时无操作。</summary>
        public void Undo()
        {
            if (_undo.Count == 0) return;
            var action = _undo.Pop();
            action.Undo();
            _redo.Push(action);
        }

        /// <summary>重做栈顶动作并转回撤销栈。栈空时无操作。</summary>
        public void Redo()
        {
            if (_redo.Count == 0) return;
            var action = _redo.Pop();
            action.Redo();
            _undo.Push(action);
        }

        /// <summary>清空两栈（如整图重载后）。</summary>
        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
