AWS EKS Cluster Setup, Deployment, Monitoring, and Cost Estimate
This README documents the end-to-end process to set up an Amazon EKS cluster, deploy a .NET API using Docker and Kubernetes, install Prometheus and Grafana for monitoring with Helm, troubleshoot common issues, and estimate costs. All commands are tailored for your setup (e.g., ap-south-1 region, single-node to multi-node scaling).

Prerequisites
Tools: AWS CLI, kubectl, eksctl, Helm, Docker, Terraform (optional for infrastructure).

AWS Account: Permissions for EKS, IAM, EC2, EBS. Configure AWS CLI with credentials (aws configure).

PowerShell Note: For multi-line commands, use backticks (`) or here-strings; some commands are shown in Bash-style but work in PowerShell with adjustments.

Docker Hub: Account for pushing images (replace username with yours).

Step 1: Create EKS Cluster
Use eksctl for quick setup or Terraform for automation.

With eksctl:

text
eksctl create cluster --name eks-practice --region ap-south-1 --nodegroup-name default-node-group --node-type t3.small --nodes 1 --nodes-min 1 --nodes-max 2
With Terraform (example main.tf snippet):

text
provider "aws" {
  region = "ap-south-1"
}

module "eks" {
  source          = "terraform-aws-modules/eks/aws"
  cluster_name    = "eks-practice"
  cluster_version = "1.31"
  subnet_ids      = ["subnet-xxx", "subnet-yyy"]  # Your VPC subnets
  vpc_id          = "vpc-xxx"

  eks_managed_node_groups = {
    default = {
      min_size     = 1
      max_size     = 2
      desired_size = 1
      instance_types = ["t3.small"]
    }
  }
}
Run terraform init && terraform apply.

Verify: kubectl get nodes.

Step 2: Build and Push .NET API Docker Image
text
docker build -t username/dotnet-api:latest ./api
docker push username/dotnet-api:latest
To update code: Rebuild and push with the same tag, then update Deployment (e.g., kubectl rollout restart deployment/dotnet-api).

Step 3: Deploy .NET API
Create k8s/deploy.yaml with your Deployment spec (include image, ports, probes).

Apply:

text
kubectl apply -f k8s/deploy.yaml
Troubleshoot:

Check Pods: kubectl get pods

Describe: kubectl describe pod <pod-name>

Logs: kubectl logs <pod-name>

Fix probes: Edit YAML to match app endpoints (e.g., path: /healthz), reapply.

Step 4: Install Prometheus with Helm
Create namespace:

text
kubectl create namespace monitoring
Add repo:

text
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update
Install (with gp3 persistence):

text
helm install prometheus prometheus-community/prometheus --namespace monitoring --set alertmanager.persistence.storageClass="gp3" --set server.persistentVolume.storageClass="gp3" --set server.persistentVolume.enabled=true
If "cannot re-use name" error: helm uninstall prometheus -n monitoring.

Verify: kubectl get pods -n monitoring.

Access: kubectl port-forward svc/prometheus-server 9090:80 -n monitoring (open http://localhost:9090).

Step 5: Troubleshoot Prometheus Installation (PVC/IAM Issues)
Create gp3 StorageClass (save as gp3-sc.yaml):

text
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: gp3
  annotations:
    storageclass.kubernetes.io/is-default-class: "true"
provisioner: ebs.csi.aws.com
parameters:
  type: gp3
  fsType: ext4
reclaimPolicy: Delete
volumeBindingMode: Immediate
allowVolumeExpansion: true
Apply: kubectl apply -f gp3-sc.yaml.

Install EBS CSI Driver:

text
eksctl create addon --name aws-ebs-csi-driver --cluster eks-practice --region ap-south-1
Attach IAM Policy:
In AWS IAM console, attach "AmazonEBSCSIDriverPolicy" to your node role (e.g., "default-eks-node-group-xxx").

Drain/Uncordon Node (to refresh permissions):

text
kubectl drain <node-name> --ignore-daemonsets --delete-emptydir-data
kubectl uncordon <node-name>
If PDBs block drain: Temporarily delete them (kubectl delete pdb coredns -n kube-system, etc.), drain, then recreate (see Step 9 for recreation commands).

Step 6: Install Grafana with Helm
Add repo:

text
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
Install (with persistence and LoadBalancer):

text
helm install grafana grafana/grafana --namespace monitoring --set persistence.enabled=true --set persistence.storageClass="gp3" --set adminPassword='your-secure-password' --set service.type=LoadBalancer
If "cannot re-use name" error: helm uninstall grafana -n monitoring.

Verify: kubectl get pods -n monitoring.

Access: kubectl port-forward svc/grafana 3000:80 -n monitoring (open http://localhost:3000).

Retrieve password (PowerShell):

text
[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String((kubectl get secret --namespace monitoring grafana -o jsonpath="{.data.admin-password}")))
Step 7: Configure Grafana with Prometheus Data Source
Log in to Grafana (username: admin, password from above).

Go to Connections > Data sources > Add data source > Prometheus.

Set URL: http://prometheus-server.monitoring.svc.cluster.local.

Save & Test.

Import dashboards (e.g., ID 3110 for Kubernetes monitoring).

Step 8: Scale EKS Node Group (for Capacity Issues)
If Pods are Pending due to "Too many pods":

Update Terraform (example):

text
resource "aws_eks_node_group" "default" {
  cluster_name    = "eks-practice"
  node_group_name = "default-eks-node-group"
  scaling_config {
    desired_size = 2
    max_size     = 3
    min_size     = 1
  }
  instance_types = ["t3.medium"]
}
Apply: terraform apply.

Or via AWS Console: Edit node group to increase desired size.

Step 9: Handling PodDisruptionBudgets (PDBs) During Drain
If drain fails due to PDB violations:

Delete temporarily:

text
kubectl delete pdb coredns -n kube-system
kubectl delete pdb ebs-csi-controller -n kube-system
Recreate in PowerShell (for CoreDNS):

text
$yaml = @'
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: coredns
  namespace: kube-system
spec:
  minAvailable: 1
  selector:
    matchLabels:
      k8s-app: kube-dns
'@

$yaml | kubectl apply -f -
For EBS CSI (similarly):

text
$yaml = @'
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: ebs-csi-controller
  namespace: kube-system
spec:
  maxUnavailable: 1
  selector:
    matchLabels:
      app: ebs-csi-controller
'@

$yaml | kubectl apply -f -
Step 10: Cost Estimate for 2 Days (September 1-2, 2025)
Based on usage (1-2 t3.small nodes, minimal EBS, control plane):

EKS Control Plane: ~₹402 (₹8.38/hour × 48 hours).

EC2 Nodes: ~₹135-180 (₹1.88/hour per node).

EBS Storage: ~₹9 (for ~20 GB gp3).

Other (e.g., data transfer, ELB): ~₹3.

Total: ~₹550 INR (check AWS Billing for exact).

Use AWS Cost Explorer or Pricing Calculator for precision.

Notes
Troubleshooting: Use kubectl describe pod <pod-name> -n monitoring for Pending issues; check events for details like PVC or resource errors.

Security: Change default passwords; use Ingress for production exposure.

Cleanup: eksctl delete cluster --name eks-practice to stop costs.

Single-Node Limits: Add nodes to avoid scheduling/PDB issues.

Replace placeholders (e.g., username, passwords, node names).
