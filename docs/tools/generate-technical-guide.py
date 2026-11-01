"""Generate the reading copy from technical-guide.md and its diagram PNGs.

Requires python-docx. Run after regenerating PNGs from any edited SVG sources.
This intentionally supports only the Markdown constructs used by this guide.
"""
from pathlib import Path
import re
import unicodedata
from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.opc.constants import RELATIONSHIP_TYPE as RT

DOCS = Path(__file__).resolve().parents[1]
OUTPUT = DOCS.parent.parent / 'Dokumentacia' / 'The Visitor technicka dokumentacia.docx'


def bookmark_name(anchor):
    value = unicodedata.normalize('NFKD', anchor).encode('ascii', 'ignore').decode()
    return 'section_' + re.sub(r'[^a-zA-Z0-9_]', '_', value)[:31]


def inline(paragraph, text):
    for token in re.split(r'(\[[^\]]+\]\([^)]+\)|`[^`]+`|\*\*[^*]+\*\*)', text):
        match = re.fullmatch(r'\[([^\]]+)\]\(([^)]+)\)', token)
        if match:
            label, target = match.groups()
            # Relative links resolve from the Word copy's output directory.
            import os
            link = OxmlElement('w:hyperlink')
            if target.startswith('#'):
                link.set(qn('w:anchor'), bookmark_name(target[1:]))
            else:
                target = os.path.relpath((DOCS / target).resolve(), OUTPUT.parent).replace('\\', '/')
                relation = paragraph.part.relate_to(target, RT.HYPERLINK, is_external=True)
                link.set(qn('r:id'), relation)
            run = OxmlElement('w:r'); props = OxmlElement('w:rPr')
            color = OxmlElement('w:color'); color.set(qn('w:val'), '24566A'); props.append(color)
            run.append(props); t = OxmlElement('w:t'); t.text = label; run.append(t)
            link.append(run); paragraph._p.append(link)
        else:
            is_code = token.startswith('`') and token.endswith('`')
            is_bold = token.startswith('**') and token.endswith('**')
            value = token[1:-1] if is_code else token[2:-2] if is_bold else token
            run = paragraph.add_run(value)
            if is_code: run.font.name = 'Consolas'; run.font.size = Pt(9)
            if is_bold: run.bold = True


def main():
    doc = Document()
    section = doc.sections[0]
    section.page_width = Cm(21); section.page_height = Cm(29.7)
    section.top_margin = section.bottom_margin = Cm(1.9)
    section.left_margin = section.right_margin = Cm(2)
    normal = doc.styles['Normal']; normal.font.name = 'Calibri'; normal.font.size = Pt(10.5)
    normal.paragraph_format.space_after = Pt(6)
    normal.paragraph_format.line_spacing = 1.08
    for name, size in [('Title', 25), ('Heading 1', 16), ('Heading 2', 12)]:
        s = doc.styles[name]; s.font.name = 'Calibri'; s.font.size = Pt(size)
        s.font.color.rgb = RGBColor(0, 0, 0)
        s.paragraph_format.keep_with_next = True
    doc.core_properties.title = 'The Visitor technická dokumentácia'
    doc.core_properties.subject = 'Technická dokumentácia projektu'
    doc.core_properties.author = 'The Visitor'
    in_code = False
    table = None
    bookmark_id = 0
    for line in (DOCS / 'technical-guide.md').read_text(encoding='utf-8').splitlines():
        if line.startswith('```'): in_code = not in_code; continue
        if in_code:
            p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(0)
            run = p.add_run(line); run.font.name = 'Consolas'; run.font.size = Pt(9)
            continue
        if not line.strip(): continue
        if line.startswith('|'):
            cells = [value.strip() for value in line.strip('|').split('|')]
            if all(re.fullmatch(r':?-+:?', value) for value in cells): continue
            if table is None:
                table = doc.add_table(rows=0, cols=len(cells))
                table.style = 'Light Shading Accent 1'
            row = table.add_row()
            for cell, value in zip(row.cells, cells):
                inline(cell.paragraphs[0], value)
            continue
        table = None
        image = re.fullmatch(r'!\[([^\]]+)\]\(([^)]+)\)', line)
        if image:
            p = doc.add_paragraph(); p.paragraph_format.keep_with_next = True
            shape = p.add_run().add_picture(str(DOCS / image[2]), width=Cm(17))
            shape._inline.docPr.set('descr', image[1])
            continue
        if line.startswith('# '): p = doc.add_paragraph(style='Title'); text = line[2:]
        elif line.startswith('## '): p = doc.add_paragraph(style='Heading 1'); text = line[3:]
        elif line.startswith('### '): p = doc.add_paragraph(style='Heading 2'); text = line[4:]
        elif line.startswith('- '): p = doc.add_paragraph(style='List Bullet'); text = line[2:]
        elif re.match(r'^\d+\. ', line):
            # Keep explicit per-recipe numbering rather than continuing Word's list counter.
            p = doc.add_paragraph(); p.paragraph_format.left_indent = Cm(.4)
            p.paragraph_format.first_line_indent = Cm(-.4); text = line
        else: p = doc.add_paragraph(); text = line
        inline(p, text)
        if line.startswith('## '):
            bookmark_id += 1
            start = OxmlElement('w:bookmarkStart')
            start.set(qn('w:id'), str(bookmark_id))
            start.set(qn('w:name'), bookmark_name(text.lower().replace(' ', '-')))
            end = OxmlElement('w:bookmarkEnd'); end.set(qn('w:id'), str(bookmark_id))
            p._p.insert(0, start); p._p.append(end)
    footer = section.footer.paragraphs[0]
    footer.alignment = 2
    fld = OxmlElement('w:fldSimple'); fld.set(qn('w:instr'), 'PAGE'); footer._p.append(fld)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUTPUT)
    print(OUTPUT)


if __name__ == '__main__':
    main()
