FROM huggingface/transformers-pytorch-gpu
WORKDIR /app
COPY . .
ENTRYPOINT ["python3", "/app/text-classification-server.py"]