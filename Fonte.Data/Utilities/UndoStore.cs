﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.Data.Utilities
{
    using Fonte.Data.Changes;
    using Fonte.Data.Interfaces;

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    class UndoStore : IUndoProvider, IUndoStore
    {
        private readonly List<IChange> _undoStack = new List<IChange>();
        private readonly List<IChange> _redoStack = new List<IChange>();
        private int _undoCounter = 0;
        private int _redoCounter = 0;
        private ChangeGroup _undoGroup;
        private int _undoGroupIndex = 0;

        public bool CanRedo => _redoStack.Count > 0 && _redoCounter > 0 && _undoGroupIndex == 0;
        
        public bool CanUndo => _undoStack.Count > 0 && _undoCounter > 0 && _undoGroupIndex == 0;

        public bool HasOpenGroup => _undoGroupIndex > 0;

        public bool IsDirty => _undoCounter > 0 || _undoGroupIndex > 0 && !_undoGroup.IsShallow;

        public bool IsEnabled { get; set; } = true;

        public IChangeGroup CreateUndoGroup()
        {
            if (_undoGroupIndex > 0)
            {
                return _undoGroup.CloneWithIndex(++_undoGroupIndex);
            }

            _undoGroup = new ChangeGroup(
                this,
                ++_undoGroupIndex
            );
            return _undoGroup;
        }

        public void Clear()
        {
            if (HasOpenGroup)
                throw new InvalidOperationException($"Cannot clear stack with an open {nameof(ChangeGroup)}");
            if (!IsEnabled)
                throw new InvalidOperationException($"Cannot clear stack while undo store is disabled");

            _undoStack.Clear();
            _undoCounter = 0;
            _redoStack.Clear();
            _redoCounter = 0;
        }

        public void ProcessChange(IChange change)
        {
            if (IsEnabled)
            {
                if (_undoGroupIndex > 0)
                {
                    _undoGroup.Add(change);
                }
                else
                {
                    if (!change.IsShallow)
                    {
                        ++_undoCounter;
                        _redoCounter = 0;
                        _redoStack.Clear();
                    }
                    _undoStack.Add(change);
                }
            }
        }

        public void Redo()
        {
            if (_redoCounter <= 0)
                throw new InvalidOperationException("Cannot redo at this time");
            if (_undoGroupIndex > 0)
                throw new InvalidOperationException("Cannot redo while in undo group (" + _undoGroupIndex + ")");

            try
            {
                IsEnabled = false;

                while (true)
                {
                    var change = _redoStack[_redoStack.Count - 1];
                    _redoStack.RemoveAt(_redoStack.Count - 1);
                    change.Apply();
                    _undoStack.Add(change);

                    if (!change.IsShallow)
                    {
                        --_redoCounter;
                        ++_undoCounter;
                        Debug.Assert(_redoCounter >= 0);
                        break;
                    }
                }
            }
            finally
            {
                IsEnabled = true;
            }
        }

        public void Undo()
        {
            if (_undoCounter <= 0)
                throw new InvalidOperationException("Cannot undo at this time");
            if (_undoGroupIndex > 0)
                throw new InvalidOperationException("Cannot undo while in undo group (" + _undoGroupIndex + ")");

            try
            {
                IsEnabled = false;

                while (true)
                {
                    var change = _undoStack[_undoStack.Count - 1];
                    _undoStack.RemoveAt(_undoStack.Count - 1);
                    change.Apply();
                    _redoStack.Add(change);

                    if (!change.IsShallow)
                    {
                        --_undoCounter;
                        ++_redoCounter;
                        Debug.Assert(_undoCounter >= 0);
                        break;
                    }
                }
            }
            finally
            {
                IsEnabled = true;
            }
        }

        public void OnUndoGroupDisposed(int index)
        {
            if (index != _undoGroupIndex)
                throw new InvalidOperationException(string.Format(
                        "Disposed undo group {0} is not the topmost one {1}",
                        index, _undoGroupIndex));
            --_undoGroupIndex;

            if (_undoGroupIndex == 0)
            {
                if (_undoGroup.Count > 0)
                {
                    if (!_undoGroup.IsShallow)
                    {
                        ++_undoCounter;
                        _redoCounter = 0;
                        _redoStack.Clear();
                    }
                    _undoStack.Add(_undoGroup);
                }
                _undoGroup = default;
            }
        }
    }
}