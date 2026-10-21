FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY server.py webapp.py branding.py .
COPY web ./web
COPY downloads ./downloads
USER 65534:65534
ENV BIND_HOST=0.0.0.0
CMD ["python", "-u", "server.py"]
