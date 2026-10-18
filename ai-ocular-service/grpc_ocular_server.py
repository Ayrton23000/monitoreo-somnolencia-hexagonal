import os
os.environ["TF_USE_LEGACY_KERAS"] = "1"
import io
from concurrent import futures
import grpc
import numpy as np
from PIL import Image, ImageOps
import tensorflow as tf
import tf_keras as keras
import ocular_detector_pb2
import ocular_detector_pb2_grpc

MODEL_PATH = "models/keras_model.h5"
LABELS_PATH = "models/labels.txt"

class CompatibleDepthwiseConv2D(keras.layers.DepthwiseConv2D):
    def __init__(self, **kwargs):
        kwargs.pop("groups", None)
        super().__init__(**kwargs)

model = keras.models.load_model(
    MODEL_PATH,
    custom_objects={"DepthwiseConv2D": CompatibleDepthwiseConv2D},
    compile=False
)

with open(LABELS_PATH, "r", encoding="utf-8") as f:
    class_names = [line.strip() for line in f.readlines() if line.strip()]

class OcularClassifierServicer(ocular_detector_pb2_grpc.OcularClassifierServicer):
    def ClasificarEstadoOcular(self, request, context):
        image = Image.open(io.BytesIO(request.imagen_bytes)).convert("RGB")
        size = (224, 224)
        image = ImageOps.fit(image, size, Image.Resampling.LANCZOS)
        image_array = np.asarray(image)
        normalized_image_array = (image_array.astype(np.float32) / 127.5) - 1.0
        data = np.ndarray(shape=(1, 224, 224, 3), dtype=np.float32)
        data[0] = normalized_image_array

        prediction = model.predict(data, verbose=0)
        index = int(np.argmax(prediction))
        confidence_score = float(prediction[0][index])
        raw_label = class_names[index].upper() if index < len(class_names) else str(index)

        ojos_cerrados = "CERRADO" in raw_label or "CLOSED" in raw_label or index == 1
        estado = "CERRADO" if ojos_cerrados else "ABIERTO"

        print(f"Fotograma procesado -> Clase: {raw_label} | Estado: {estado} | Confianza: {confidence_score:.2f}")

        return ocular_detector_pb2.PrediccionOcularReply(
            estado_ocular=estado,
            confianza=confidence_score,
            ojos_cerrados=ojos_cerrados
        )

def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    ocular_detector_pb2_grpc.add_OcularClassifierServicer_to_server(
        OcularClassifierServicer(), server
    )
    server.add_insecure_port("[::]:50051")
    server.start()
    print("Servidor gRPC con keras_model.h5 activo en puerto 50051")
    server.wait_for_termination()

if __name__ == "__main__":
    serve()