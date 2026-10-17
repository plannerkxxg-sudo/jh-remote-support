"""Plain-text presentation settings; never an identity or authorization mechanism."""
import html
import os
import re
from urllib.parse import urlencode


DEFAULT_DESCRIPTION = "화면을 보며 홈페이지 가입과 신청 절차를 도와드립니다."


def clean_text(value, limit):
    if not isinstance(value, str) or len(value) > limit:
        return None
    return "".join(c for c in value.strip() if c.isprintable())


def settings(query=None):
    query = query or {}
    # Personalized links do not modify the deployment or other teachers' pages.
    name = clean_text(query.get("name", [os.getenv("SUPPORT_NAME", "")])[0], 40)
    description = clean_text(query.get("description", [os.getenv("SUPPORT_DESCRIPTION", "")])[0], 200)
    if name is None or description is None:
        raise ValueError("이름은 40자, 소개 문구는 200자 이내로 입력하세요.")
    return {
        "name": name,
        "title": (name + " 원격지원") if name else "원격지원",
        "description": description or DEFAULT_DESCRIPTION,
    }


def render(template, brand):
    owner = brand["name"] or "담당자"
    home = "/?" + urlencode({"name": brand["name"], "description": brand["description"]})
    values = {
        "SITE_TITLE": brand["title"], "SUPPORT_NAME": owner,
        "SITE_DESCRIPTION": brand["description"], "SITE_HOME": home,
    }
    # Single substitution pass: user-provided template-like text stays plain text.
    return re.sub(r"\{\{(SITE_TITLE|SUPPORT_NAME|SITE_DESCRIPTION|SITE_HOME)\}\}",
                  lambda match: html.escape(values[match.group(1)], quote=True), template)
