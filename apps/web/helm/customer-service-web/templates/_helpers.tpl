{{/*
Chart name, truncated to the 63-character DNS label limit.
*/}}
{{- define "customer-service-web.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Fully qualified app name. Uses the release name alone when it already contains the chart name.
*/}}
{{- define "customer-service-web.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "customer-service-web.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "customer-service-web.labels" -}}
helm.sh/chart: {{ include "customer-service-web.chart" . }}
{{ include "customer-service-web.selectorLabels" . }}
app.kubernetes.io/version: {{ .Values.image.tag | default .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "customer-service-web.selectorLabels" -}}
app.kubernetes.io/name: {{ include "customer-service-web.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "customer-service-web.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "customer-service-web.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{- define "customer-service-web.image" -}}
{{- printf "%s:%s" .Values.image.repository (.Values.image.tag | default .Chart.AppVersion) }}
{{- end }}
