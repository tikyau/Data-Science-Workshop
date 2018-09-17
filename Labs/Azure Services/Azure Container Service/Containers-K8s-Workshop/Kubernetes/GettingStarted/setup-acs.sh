#! /bin/sh

# Creat Resource Group
az group create --name K8sCluster --location Eastus2

# Create Cluster
#az acs create --orchestrator-type kubernetes --resource-group myK8sCluster --name myK8sCluster --service-principal <client_id> --client-secret <client_secret> --agent-vm-size Standard_DS3_v2 --agent-count 2 --generate-ssh-keys
az acs create --orchestrator-type kubernetes --resource-group K8sCluster --name KCluster --master-vm-size Standard_DS2_v2 --agent-vm-size Standard_DS2_v2 --agent-count 2 --ssh-key-value ~/.ssh/id_rsa.pub

# Must for first time only ; Install Kubectl CLI. If you are using Windows than kubectl is in program files (x86). Make sure it is in your PATH variable
az acs kubernetes install-cli

# Connect kubectl to cluster
az acs kubernetes get-credentials --resource-group=K8sCluster --name=KCluster

# Proxy to the dashboard
kubectl proxy
