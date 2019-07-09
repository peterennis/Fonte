﻿
namespace Fonte.Data.Changes
{
    using Fonte.Data.Interfaces;

    using System.Collections.Generic;

    internal struct LayerAnchorsChange : IChange
    {
        private readonly Layer _parent;
        private readonly int _index;
        private bool _insert;
        private readonly IList<Anchor> _items;

        public bool AffectsSelection => true;
        public bool IsShallow => false;

        public LayerAnchorsChange(Layer parent, int index, IList<Anchor> items, bool insert)
        {
            _parent = parent;
            _index = index;
            _insert = insert;
            _items = items;
        }

        public void Apply()
        {
            var items = _parent._anchors;
            if (_insert)
            {
                items.InsertRange(_index, _items);
                foreach (var item in _items) { item.Parent = _parent; }
            }
            else
            {
                items.RemoveRange(_index, _items.Count);
                foreach (var item in _items) { item.Parent = null; }
            }
            _insert = !_insert;

            _parent.OnChange(this);
        }
    }
}
