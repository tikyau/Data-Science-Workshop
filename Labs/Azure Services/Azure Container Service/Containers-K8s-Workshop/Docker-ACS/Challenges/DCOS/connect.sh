#! /bin/sh
rg=dcosworkshop
acsname=myDCOSCluster

# connect to dcos
ip=$(az network public-ip list --resource-group $rg --query "[?contains(name,'dcos-master')].[ipAddress]" -o tsv)
sudo ssh -i ~/.ssh/id_rsa -fNL 8083:localhost:80 -p 2200 azureuser@mydcosclus-dcosworkshop-54a522mgmt.canadacentral.cloudapp.azure.com

# or using the CLI
az acs dcos browse --name $acsname --resource-group $rg --ssh-key-file ~/.ssh/id_rsa

