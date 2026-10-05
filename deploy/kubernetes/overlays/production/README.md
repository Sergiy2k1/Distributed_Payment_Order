# Production overlay

This overlay is the production-oriented Kubernetes configuration for PayFlow.

## Before deployment

1. Ensure the six GHCR images referenced by `kustomization.yaml` exist.
2. Create the required Secrets described in `../../base/secrets.example.yaml` through a secret manager or CI/CD pipeline. Do not apply the example file as-is.
3. Ensure the external platform dependencies referenced by the base ConfigMaps are reachable:
   - Kafka at `kafka.payflow-infra.svc.cluster.local:9092`.
   - OpenTelemetry Collector at `otel-collector.payflow-observability.svc.cluster.local:4317`.
   - Service-owned PostgreSQL databases through the connection strings supplied in Secrets.

## Render

```bash
kubectl kustomize deploy/kubernetes/overlays/production
```

## Apply

```bash
kubectl apply -k deploy/kubernetes/overlays/production
```

The production overlay pins images to immutable Git SHA tags. Update all six tags together to a CI-published release commit before each rollout.

Order API and Payment API run with two replicas and have PodDisruptionBudgets with `minAvailable: 1`. Background workers and the mock payment provider remain single-replica until horizontal scaling is validated by load and failure testing.
