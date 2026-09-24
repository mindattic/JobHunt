// A small schema-driven form engine for the Applicant and Job Requirements tabs. A schema is a list
// of nodes; the form edits a deep copy of the data in place and collect() hands it back.
//   field:  { key, label, type, options?, help?, wide? }   type: text | textarea | number | bool |
//           select | tags | multi | month | email | tel | url | password
//   group:  { group: 'Title', key?, fields: [...] }         key descends into a sub-object
//   list:   { list: 'Title', key, item: 'Noun', title: fn(item), fields: [...], make: fn() }
// Every value is written with .value / .checked / textContent — nothing is ever parsed as HTML.
(function () {
  'use strict';
  var JH = window.JH = window.JH || {};

  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    Object.keys(attrs || {}).forEach(function (k) {
      if (k === 'text') node.textContent = attrs[k];
      else if (k === 'class') node.className = attrs[k];
      else if (attrs[k] !== undefined && attrs[k] !== null) node.setAttribute(k, attrs[k]);
    });
    (children || []).forEach(function (c) { if (c) node.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
    return node;
  }
  JH.el = el;

  var uid = 0;
  function nextId() { return 'f' + (++uid); }

  function clone(v) { return v === undefined ? undefined : JSON.parse(JSON.stringify(v)); }
  JH.clone = clone;

  /** Builds the form into `container`. By default it edits a copy of `data`; pass inPlace to edit
   *  `data` itself (several forms sharing one record). onChange fires on every edit. */
  JH.buildForm = function (container, schema, data, onChange, inPlace) {
    var model = inPlace ? data : (clone(data) || {});
    container.textContent = '';
    renderNodes(container, schema, model, onChange || function () {});
    return { collect: function () { return model; }, model: model };
  };

  function renderNodes(parent, nodes, obj, changed) {
    var grid = null;
    nodes.forEach(function (n) {
      if (n.group || n.list) { grid = null; }
      if (n.group) return renderGroup(parent, n, obj, changed);
      if (n.list) return renderList(parent, n, obj, changed);
      if (!grid) { grid = el('div', { class: 'form-grid' }); parent.appendChild(grid); }
      grid.appendChild(renderField(n, obj, changed));
    });
  }

  function renderGroup(parent, n, obj, changed) {
    var target = obj;
    if (n.key) { obj[n.key] = obj[n.key] || {}; target = obj[n.key]; }
    var box = el('fieldset', { class: 'group' }, [el('legend', { text: n.group })]);
    if (n.help) box.appendChild(el('p', { class: 'muted help', text: n.help }));
    renderNodes(box, n.fields, target, changed);
    parent.appendChild(box);
  }

  function renderList(parent, n, obj, changed) {
    obj[n.key] = obj[n.key] || [];
    var items = obj[n.key];
    var wrap = el('div', { class: 'list' });
    parent.appendChild(wrap);

    function draw() {
      wrap.textContent = '';
      if (n.help) wrap.appendChild(el('p', { class: 'muted help', text: n.help }));
      items.forEach(function (item, i) {
        var title = (n.title && n.title(item)) || (n.item + ' ' + (i + 1));
        var head = el('div', { class: 'list-head' }, [el('strong', { text: title })]);
        var tools = el('div', { class: 'list-tools' });
        if (i > 0) tools.appendChild(button('↑', 'Move up', function () { move(i, -1); }));
        if (i < items.length - 1) tools.appendChild(button('↓', 'Move down', function () { move(i, 1); }));
        tools.appendChild(button('Remove', 'Remove this ' + n.item.toLowerCase(), function () {
          items.splice(i, 1); changed(); draw();
        }));
        head.appendChild(tools);
        var card = el('div', { class: 'list-item' }, [head]);
        renderNodes(card, n.fields, item, function () {
          changed();
          head.firstChild.textContent = (n.title && n.title(item)) || (n.item + ' ' + (i + 1));
        });
        wrap.appendChild(card);
      });
      wrap.appendChild(button('+ Add ' + n.item.toLowerCase(), null, function () {
        items.push(n.make ? n.make() : {}); changed(); draw();
        var cards = wrap.querySelectorAll('.list-item');
        var first = cards[cards.length - 1] && cards[cards.length - 1].querySelector('input, textarea, select');
        if (first) first.focus();
      }, 'add'));
    }
    function move(i, d) {
      var t = items[i]; items[i] = items[i + d]; items[i + d] = t; changed(); draw();
    }
    draw();
  }

  function button(text, label, onClick, cls) {
    var b = el('button', { type: 'button', text: text, class: cls || 'small', 'aria-label': label || undefined });
    b.addEventListener('click', onClick);
    return b;
  }
  JH.button = button;

  function renderField(f, obj, changed) {
    var id = nextId();
    var wrap = el('div', { class: 'field' + (f.wide || f.type === 'textarea' || f.type === 'tags' || f.type === 'multi' ? ' wide' : '') });
    var value = obj[f.key];

    if (f.type === 'bool') {
      var box = el('input', { type: 'checkbox', id: id });
      box.checked = !!value;
      box.addEventListener('change', function () { obj[f.key] = box.checked; changed(); });
      wrap.appendChild(el('label', { class: 'check', for: id }, [box, ' ' + f.label]));
      return withHelp(wrap, f);
    }

    if (f.type === 'multi') {
      // A group of checkboxes has no single control to point a <label for> at — name the group.
      wrap.appendChild(el('span', { id: id, class: 'group-label', text: f.label }));
      var set = Array.isArray(value) ? value.slice() : [];
      obj[f.key] = set;
      var row = el('div', { class: 'checks', role: 'group', 'aria-labelledby': id });
      f.options.forEach(function (o) {
        var cid = nextId();
        var c = el('input', { type: 'checkbox', id: cid });
        c.checked = set.indexOf(o.value) >= 0;
        c.addEventListener('change', function () {
          var at = set.indexOf(o.value);
          if (c.checked && at < 0) set.push(o.value);
          if (!c.checked && at >= 0) set.splice(at, 1);
          changed();
        });
        row.appendChild(el('label', { class: 'check', for: cid }, [c, ' ' + o.label]));
      });
      wrap.appendChild(row);
      return withHelp(wrap, f);
    }

    wrap.appendChild(el('label', { for: id, text: f.label }));

    if (f.type === 'tags') {
      wrap.appendChild(tagInput(id, obj, f, changed));
      return withHelp(wrap, f);
    }

    var input;
    if (f.type === 'textarea') {
      input = el('textarea', { id: id, rows: f.rows || 4 });
      input.value = value == null ? '' : value;
    } else if (f.type === 'select') {
      input = el('select', { id: id });
      (f.options || []).forEach(function (o) {
        var opt = typeof o === 'string' ? { value: o, label: o || '—' } : o;
        input.appendChild(el('option', { value: opt.value, text: opt.label }));
      });
      input.value = value == null ? '' : value;
    } else if (f.type === 'month') {
      // Plain text, not <input type=month>: a saved "2019" (year only, common in imports) would show
      // as blank there and read as "current". Accepts YYYY or YYYY-MM.
      input = el('input', { id: id, type: 'text', inputmode: 'numeric', autocomplete: 'off',
        placeholder: 'YYYY-MM', pattern: '\\d{4}(-(0[1-9]|1[0-2]))?', maxlength: '7' });
      input.value = value == null ? '' : value;
    } else {
      // Whole numbers unless the field says otherwise (a fractional value can't go into a count).
      var whole = f.type === 'number' && !f.decimal && !(f.step && String(f.step).indexOf('.') >= 0);
      input = el('input', { id: id, type: f.type === 'number' ? 'number' : f.type || 'text', step: f.step || (whole ? '1' : 'any'),
        min: f.min, max: f.max, autocomplete: f.autocomplete || 'off', placeholder: f.placeholder });
      input.value = value == null ? '' : value;
      input.dataset.whole = whole ? '1' : '';
    }
    input.addEventListener(f.type === 'select' ? 'change' : 'input', function () {
      if (f.type === 'number') {
        var n = input.value === '' ? null : Number(input.value);
        if (n != null && !isFinite(n)) n = null;
        obj[f.key] = n != null && input.dataset.whole ? Math.round(n) : n;
      }
      else obj[f.key] = input.value;
      changed();
    });
    wrap.appendChild(input);
    return withHelp(wrap, f);
  }

  /** Help text is tied to its control with aria-describedby, so it is read with the field. */
  function withHelp(wrap, f) {
    if (!f.help) return wrap;
    var helpId = nextId();
    wrap.appendChild(el('div', { class: 'muted help', id: helpId, text: f.help }));
    var control = wrap.querySelector('input, select, textarea, [role=group]');
    if (control) control.setAttribute('aria-describedby', helpId);
    return wrap;
  }

  /** Keyword chips: type and press Enter (or a comma) to add; × to remove; pasted lists split. */
  function tagInput(id, obj, f, changed) {
    var list = Array.isArray(obj[f.key]) ? obj[f.key] : [];
    obj[f.key] = list;
    var box = el('div', { class: 'tags' });
    var input = el('input', { id: id, type: 'text', placeholder: f.placeholder || 'Type and press Enter', autocomplete: 'off' });

    function draw() {
      Array.prototype.slice.call(box.querySelectorAll('.tag')).forEach(function (t) { t.remove(); });
      list.forEach(function (t, i) {
        var x = el('button', { type: 'button', class: 'tag-x', 'aria-label': 'Remove ' + t, text: '×' });
        x.addEventListener('click', function () { list.splice(i, 1); changed(); draw(); input.focus(); });
        box.insertBefore(el('span', { class: 'tag' }, [t, x]), input);
      });
    }
    function add(raw) {
      raw.split(/[,\n]/).map(function (s) { return s.trim(); }).filter(Boolean).forEach(function (t) {
        if (list.indexOf(t) < 0) list.push(t);
      });
      changed(); draw();
    }
    input.addEventListener('keydown', function (e) {
      if ((e.key === 'Enter' || e.key === ',') && input.value.trim()) { e.preventDefault(); add(input.value); input.value = ''; }
      else if (e.key === 'Backspace' && !input.value && list.length) { list.pop(); changed(); draw(); }
    });
    input.addEventListener('blur', function () { if (input.value.trim()) { add(input.value); input.value = ''; } });
    input.addEventListener('paste', function (e) {
      var text = (e.clipboardData || window.clipboardData).getData('text');
      if (/[,\n]/.test(text)) { e.preventDefault(); add(text); }
    });
    box.appendChild(input);
    box.addEventListener('click', function (e) { if (e.target === box) input.focus(); });
    draw();
    return box;
  }
})();
