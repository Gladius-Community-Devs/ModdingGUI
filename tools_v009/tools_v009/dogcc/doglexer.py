#!/usr/bin/env python3
"""doglexer.py - tokenizer for Gladius DogCode (.scp) scripts.

Reconstructed from corpus + the compiler lexer behaviour:
- whitespace insignificant; // line comments
- identifiers and keywords case-insensitive (the compiler folds case; we keep
  the original spelling for symbol names though - verify)
- numbers: ints, floats ('.5', '5.', '1.5', maybe exponents)
- string literals "...": backslash escapes \n \t \" \' \\ are decoded later by
  FixupStringControlChars (we decode on the fly and store the raw content);
  gender codes '##<...>' also legal inside strings.
- operators/punctuation incl. '...', '@', '..'? and all C-like tokens.
"""
import re

KEYWORDS = {
    'function', 'endfunction', 'if', 'elseif', 'else', 'endif', 'while',
    'endwhile', 'for', 'to', 'step', 'next', 'switch', 'endswitch', 'case',
    'default', 'break', 'continue', 'return', 'shared', 'global', 'struct',
    'structure', 'endstructure', 'vector', 'article', 'string', 'array',
    'call', 'goto', 'and', 'or', 'not', 'do', 'true', 'false', 'null',
    'none', 'self', 'int', 'float',
}

# ordered multi-char operators (longest first)
OPS = [
    '<<=', '>>=', '...', '>=', '<=', '!=', '==', '++', '--', '+=', '-=',
    '*=', '/=', '%=', '&=', '|=', '^=', '&&', '||', '->',
    '+', '-', '*', '/', '%', '&', '|', '^', '!', '<', '>', '=', '.', ',',
    '(', ')', '[', ']', '{', '}', ':', '@', '~',
]

class Token:
    __slots__ = ('kind', 'value', 'line', 'col', 'raw')
    def __init__(self, kind, value, line, col, raw=None):
        self.kind = kind      # 'id','num','str','op','eof'
        self.value = value    # str lower for keywords/ids? keep original in raw
        self.line = line
        self.col = col
        self.raw = raw
    def __repr__(self):
        return f"Token({self.kind},{self.value!r},{self.line})"

def tokenize(text):
    tokens = []
    i, n = 0, len(text)
    line = 1
    col = 1

    def adv(k=1):
        nonlocal i, col, line
        for _ in range(k):
            if i < n and text[i] == '\n':
                line += 1
                col = 1
            else:
                col += 1
            i += 1

    while i < n:
        c = text[i]
        # whitespace
        if c in ' \t\r\n\f\v':
            adv()
            continue
        # filler tokens: ';' '?' "'" '`' and stray '\' act as pure separators
        if c == ';' or c == '?' or c == "'" or c == '`' or c == '\\':
            adv()
            continue
        # line comment
        if c == '/' and i + 1 < n and text[i+1] == '/':
            while i < n and text[i] != '\n':
                adv()
            continue
        # string literal
        if c == '"':
            start_line, start_col = line, col
            adv()
            buf = []
            while True:
                if i >= n:
                    raise SyntaxError(f"unterminated string at line {start_line}")
                c2 = text[i]
                if c2 == '"':
                    adv()
                    break
                if c2 == '\\':
                    # keep escape sequence raw; decode later
                    adv()
                    if i < n:
                        buf.append('\\')
                        buf.append(text[i])
                        adv()
                    else:
                        raise SyntaxError(f"unterminated escape at line {start_line}")
                else:
                    buf.append(c2)
                    adv()
            tokens.append(Token('str', ''.join(buf), start_line, start_col))
            continue
        # numbers
        if c.isdigit() or (c == '.' and i + 1 < n and text[i+1].isdigit()):
            start_line, start_col = line, col
            m = re.match(
                r'0[xX][0-9a-fA-F]+'
                r'|[0-9]+(\.[0-9]*)?([eE][+-]?[0-9]+)?'
                r'|\.[0-9]+([eE][+-]?[0-9]+)?', text[i:])
            if m:
                raw = m.group(0)
                adv(len(raw))
                tokens.append(Token('num', raw, start_line, start_col, raw))
                continue
        # identifiers / keywords
        if c.isalpha() or c == '_':
            start_line, start_col = line, col
            j = i
            while j < n and (text[j].isalnum() or text[j] == '_'):
                j += 1
            word = text[i:j]
            adv(j - i)
            tokens.append(Token('id', word, start_line, start_col, word))
            continue
        # operators
        matched = None
        for op in OPS:
            if text.startswith(op, i):
                matched = op
                break
        if matched:
            tokens.append(Token('op', matched, line, col))
            adv(len(matched))
            continue
        raise SyntaxError(f"unexpected char {c!r} at line {line} col {col}")
    tokens.append(Token('eof', '', line, col))
    return tokens
