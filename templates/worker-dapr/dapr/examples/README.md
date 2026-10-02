# Component examples

Copy a file over the matching one in `dapr/components/` (locally) or apply it to your cluster (Kubernetes), keeping the **component name**. The worker code doesn't change.

| Component name | File | Platform |
|----------------|------|----------|
| `events` | `events.kafka.yaml` | Apache Kafka, Redpanda, Confluent |
| `events` | `events.azure-eventhubs.yaml` | Azure Event Hubs |
| `events` | `events.azure-servicebus.yaml` | Azure Service Bus topics |
| `events` | `events.aws-snssqs.yaml` | AWS SNS + SQS |
| `events` | `events.gcp-pubsub.yaml` | Google Cloud Pub/Sub |
| `object-created` | `object-created.aws-sqs.yaml` | S3 event notifications to SQS |
| `object-created` | `object-created.azure-servicebus.yaml` | Blob Storage to Event Grid to a Service Bus queue |
| `object-created` | `object-created.gcp-pubsub.yaml` | GCS Pub/Sub notifications |
| `object-storage` | `object-storage.aws-s3.yaml` | Amazon S3 or any S3-compatible store (Ceph, RustFS, SeaweedFS...) |
| `object-storage` | `object-storage.azure-blob.yaml` | Azure Blob Storage (set `KeyMetadataName=blobName`) |
| `object-storage` | `object-storage.gcp-storage.yaml` | Google Cloud Storage |

Secrets use `secretKeyRef` against a Dapr secret store (on Kubernetes, the built-in `kubernetes` store reads Kubernetes Secrets). Prefer workload identity (Azure managed identity, AWS IRSA or Pod Identity, GCP Workload Identity) over keys. Field names follow the Dapr 1.18 component reference, so check <https://docs.dapr.io/reference/components-reference/> when you upgrade Dapr.
