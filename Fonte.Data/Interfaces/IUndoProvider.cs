﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.Data.Interfaces
{
    public interface IUndoProvider
    {
        bool CanRedo { get; }
        bool CanUndo { get; }
        bool HasOpenGroup { get; }

        IChangeGroup CreateUndoGroup();
        void Redo();
        void Undo();
    }
}
